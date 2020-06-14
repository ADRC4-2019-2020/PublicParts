using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Diagnostics;
using System.IO;

/// <summary>
/// The Grid / Slab Object that contains all the parts along with the geometry it represents
/// </summary>
public class VoxelGrid : MonoBehaviour
{
    // 
    // Fields and Parameters
    //

    // Original fields and parameters (AVOID CHANGING) 
    public Vector3Int Size { get; private set; }
    public Voxel[,,] Voxels { get; private set; }
    public Face[][,,] Faces { get; private set; }
    public float VoxelSize { get; private set; }
    public Vector3 Origin { get; private set; }

    //Migration Parameters

    //Grid setup
    //Currently available slabs: 44_44_A, 50_32_C, 38_26_C, 44_32_C
    string _gridName = "44_44";
    string _gridType = "A";
    public GameObject GridGO { get; private set; }
    //Seed to run the population method
    int _popSeed = 5;

    bool _showVoxels = true;

    //Pix2pix inference object
    PP_pix2pix _pix2pix;

    //Grid data and objects collections
    public List<Part> ExistingParts { get; private set; }
    public List<PPSpace> Spaces { get; private set; }
    public List<Voxel> Boundaries { get; private set; }

    // 
    // Methods and Functions
    //

    // Original methods and Functions (AVOID CHANGING)
    
    /// <summary>
    /// Original Grid constructor
    /// </summary>
    /// <param name="size">The vector representing the size of the grid</param>
    /// <param name="voxelSize">The size of the voxel in metres</param>
    /// <param name="origin">The origin of the grid to be created</param>
    public VoxelGrid(Vector3Int size, float voxelSize, Vector3 origin)
    {
        Size = size;
        VoxelSize = voxelSize;
        Origin = origin;
        Faces = new Face[3][,,];
        ExistingParts = new List<Part>();
        Spaces = new List<PPSpace>();
        Boundaries = new List<Voxel>();

        SetupVoxels();

    }

    /// <summary>
    /// Gets the list of active voxels of the grid
    /// </summary>
    /// <returns></returns>
    public List<Voxel> ActiveVoxelsAsList()
    {
        List<Voxel> outList = new List<Voxel>();
        for (int x = 0; x < Size.x; x++)
        {
            for (int y = 0; y < Size.y; y++)
            {
                for (int z = 0; z < Size.z; z++)
                {
                    if (Voxels[x, y, z].IsActive) outList.Add(Voxels[x, y, z]);
                }
            }
        }

        return outList;
    }

    /// <summary>
    /// Gets the boundary voxels of the grid
    /// </summary>
    /// <returns>The voxels as an IEnumerable</returns>
    public IEnumerable<Voxel> GetBoundaryVoxels()
    {
        //This method returns the voxels that are part of the boundary
        //Active, not Occupied, has at least one neighbour which is not active
        //Using linq, performance should eventually be checked
        for (int x = 0; x < Size.x; x++)
        {
            for (int y = 0; y < Size.y; y++)
            {
                for (int z = 0; z < Size.z; z++)
                {
                    Voxel voxel = Voxels[x, y, z];
                    if (voxel.IsActive && !voxel.IsOccupied && voxel.GetFaceNeighbours().Any(n => !n.IsActive))
                    {
                        yield return voxel;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Clears the grid of occupied voxels
    /// </summary>
    public void ClearGrid()
    {
        for (int x = 0; x < Size.x; x++)
        {
            for (int y = 0; y < Size.y; y++)
            {
                for (int z = 0; z < Size.z; z++)
                {
                    var v = Voxels[x, y, z];
                    if (v.IsActive && v.IsOccupied && v.Part.Type != PartType.Structure && v.Part.Type != PartType.Knot)
                    {
                        v.IsOccupied = false;
                        v.Part = null;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets faces of the grid (from https://github.com/ADRC4/Voxel)
    /// </summary>
    /// <returns></returns>
    public IEnumerable<Face> GetFaces()
    {
        for (int n = 0; n < 3; n++)
        {
            int xSize = Faces[n].GetLength(0);
            int ySize = Faces[n].GetLength(1);
            int zSize = Faces[n].GetLength(2);

            for (int x = 0; x < xSize; x++)
                for (int y = 0; y < ySize; y++)
                    for (int z = 0; z < zSize; z++)
                    {
                        yield return Faces[n][x, y, z];
                    }
        }
    }

    //Migration methods and Functions

    /// <summary>
    /// Creates a VoxelGrid instance from a file with its configurations
    /// </summary>
    /// <param name="gridName">The name of the grid file</param>
    /// <param name="gridType">The name of the grid type</param>
    public VoxelGrid(string gridName, string gridType, float voxelSize, Vector3 origin)
    {
        VoxelSize = voxelSize;
        Origin = origin;
        Faces = new Face[3][,,];
        ExistingParts = new List<Part>();
        Spaces = new List<PPSpace>();
        Boundaries = new List<Voxel>();

        _pix2pix = new PP_pix2pix();

        _gridName = gridName;
        _gridType = gridType;

        //Read one state from folder
        DirectoryInfo folder = new DirectoryInfo(Application.dataPath + $"/Resources/Input Data/TrainingData/{_gridName}");
        string[] dimensions = folder.Name.Split('_');
        int xSize = int.Parse(dimensions[0]);
        int zSize = int.Parse(dimensions[1]);

        Size = new Vector3Int(xSize, 1, zSize);
        SetupVoxels();
        //_grid = new VoxelGrid(_gridSize, _voxelSize, Vector3.zero);

        //set the states of the voxel grid
        string statesFile = "Input Data/TrainingData/" + _gridName + "/" + _gridType + "_SlabStates";
        CSVReader.SetGridState(this, statesFile);

        //move camera pivot to grid center
        //_cameraPivot.position = new Vector3(Size.x / 2, 0, Size.z / 2) * VoxelSize;

        //populate structure
        string structureFile = "Input Data/TrainingData/" + _gridName + "/" + _gridType + "_Structure";
        ReadStructure(structureFile);

        InstantiateGridGO();
    }

    /// <summary>
    /// Executes the internal Barracuda Pix2pix model, inferring from GPU and generating the respective spaces
    /// </summary>
    public void RunPopulationAndAnalysis()
    {
        Stopwatch aiStopwatch = new Stopwatch();
        aiStopwatch.Start();
        PopulateAndAnalyseGrid();
        GenerateSpaces();
        aiStopwatch.Stop();
        var t = aiStopwatch.ElapsedMilliseconds;
        //_activityLog = $"AI Message: Generated {_spaces.Count} Spaces in {t} ms";
    }

    /// <summary>
    /// Generate spaces on the voxels that are not inside the parts boudaries, or space or part
    /// The method is inspired by a BFS algorithm, continuously checking the neighbours of the
    /// processed voxels until the minimum area is reached. It is designed to be called in a loop 
    /// that feeds the numbering / naming of the spaces
    /// </summary>
    /// <param name="number">Current number of the space</param>
    private void GenerateSingleSpace(int number)
    {
        int maximumArea = 1000; //in voxel ammount
        var availableVoxels = ActiveVoxelsAsList().Where(v => !Boundaries.Contains(v) && !v.IsOccupied && !v.InSpace).ToList();
        if (availableVoxels.Count == 0) return;
        Voxel originVoxel = availableVoxels[0];

        //Initiate a new space
        PPSpace space = new PPSpace(this);
        originVoxel.InSpace = true;
        originVoxel.ParentSpace = space;
        space.Voxels.Add(originVoxel);
        space.Indices.Add(originVoxel.Index);
        //Keep running until the space area is under the minimum
        while (space.Voxels.Count < maximumArea)
        {
            List<Voxel> temp = new List<Voxel>();
            foreach (var voxel in space.Voxels)
            {
                //Get the face neighbours which are available
                var newNeighbours = voxel.GetFaceNeighbours().Where(n => availableVoxels.Contains(n));
                foreach (var neighbour in newNeighbours)
                {
                    var nIndex = neighbour.Index;
                    var gridVoxel = Voxels[nIndex.x, nIndex.y, nIndex.z];
                    //Only add the nighbour it its not already in the space 
                    //or temp list, is active, not occupied(in a part), or another space
                    if (!space.Voxels.Contains(neighbour) && !temp.Contains(neighbour))
                    {
                        if (gridVoxel.IsActive && !gridVoxel.IsOccupied && !gridVoxel.InSpace) temp.Add(neighbour);
                    }
                }
            }
            //Break if the temp list returned empty
            if (temp.Count == 0) break;
            //Add found neighbours to the space until it reaches maximum capacity
            foreach (var v in temp)
            {
                if (space.Voxels.Count <= maximumArea)
                {
                    v.InSpace = true;
                    v.ParentSpace = space;
                    space.Voxels.Add(v);
                    space.Indices.Add(v.Index);
                }
            }
        }
        space.Name = $"Space_{number.ToString()}";
        space.CreateArrow();
        Spaces.Add(space);
    }

    /// <summary>
    /// Generate spaces by looping <see cref="GenerateSingleSpace(int)"/> until all voxels are 
    /// assigned a space
    /// </summary>
    private void GenerateSpaces()
    {
        //Destroy existing spaces
        foreach (var space in Spaces) space.DestroySpace();

        //Clear spaces list
        Spaces = new List<PPSpace>();

        int i = 0;
        //Generate spaces on vacant voxels inside boundaries
        while (ActiveVoxelsAsList().Any(v => !Boundaries.Contains(v) && !v.IsOccupied && !v.InSpace))
        {
            GenerateSingleSpace(i++);
        }


        //Allocate boundary voxel to the smallest neighbouring space
        while (Boundaries.Any(b => !b.InSpace))
        {
            Voxels2SmallestNeighbour(Boundaries.Where(b => !b.InSpace));
        }

        //_activityLog = $"AI Message: Generated {Spaces.Count} Spaces";
    }

    /// <summary>
    /// Adds orphan voxels to the smallest neighbouring space
    /// </summary>
    /// <param name="voxels2Allocate">The voxels to be alocated</param>
    private void Voxels2SmallestNeighbour(IEnumerable<Voxel> voxels2Allocate)
    {
        //This method allocates the voxels in a list 
        //to the smallest neighbouring space
        var boundaryNonAllocated = voxels2Allocate;
        foreach (var voxel in boundaryNonAllocated)
        {
            var neighbours = voxel.GetFaceNeighbours();
            if (neighbours.Any(v => v.InSpace))
            {
                var closestSpace = neighbours.Where(v => v.InSpace).MinBy(s => s.ParentSpace.Voxels.Count).ParentSpace;
                if (closestSpace != null)
                {
                    closestSpace.Voxels.Add(voxel);
                    voxel.ParentSpace = closestSpace;
                    voxel.InSpace = true;
                }
            }
        }
    }

    /// <summary>
    /// Instanciates the GameObject that represents the grid
    /// </summary>
    private void InstantiateGridGO()
    {
        //instantiate grid GO
        GameObject reference = Resources.Load<GameObject>($"GameObjects/{_gridName}_{_gridType}");
        GridGO = Instantiate(reference);
        GridGO.transform.position = Vector3.zero;
        GridGO.transform.localScale = new Vector3(VoxelSize, VoxelSize, VoxelSize);
        GridGO.SetActive(!_showVoxels);
    }

    /// <summary>
    /// Condensed method to populate the grid with configurable parts and analyse it with
    /// the Pix2Pix model
    /// </summary>
    private void PopulateAndAnalyseGrid()
    {
        //Clean the boundaries
        Boundaries = new List<Voxel>();

        //populate configurables
        int componentCount = ActiveVoxelsAsList().Count(v => !v.IsOccupied) / 120;

        //Populate grid and get resulting texture, already upscaled
        var gridImage = PopulateRandomConfigurableGetImage(componentCount);

        //Analyse grid with Pix2pix
        var analysisResult = _pix2pix.GeneratePrediction(gridImage);

        //Post-process the analysis result texture
        var resultTexture = ProcessAnalysisResult(analysisResult);

        //Assign result pixels to voxel data, populating the boundaries list
        PassBoundaryToList(resultTexture);
        //_activityLog = $"AI Message: Generated {componentCount} components";
    }

    /// <summary>
    /// Process analysis result, downscaling and post-processing it, preparing the texture to be written to the grid
    /// </summary>
    /// <param name="analysisResult">Analysis result texture</param>
    /// <returns></returns>
    private Texture2D ProcessAnalysisResult(Texture2D analysisResult)
    {
        //Downscale the analysis result
        TextureScale.Point(analysisResult, 64, 64);

        //Create new texture with the same size as the original grid
        Texture2D resultGridTexture = new Texture2D(Size.x, Size.z);

        //Write result to texture
        for (int i = 0; i < resultGridTexture.width; i++)
        {
            for (int j = 0; j < resultGridTexture.height; j++)
            {
                int x = i;
                int y = analysisResult.height - resultGridTexture.height + j + 1;
                resultGridTexture.SetPixel(i, j, analysisResult.GetPixel(x, y));
            }
        }
        resultGridTexture.Apply();

        //Return post-processed texture
        return PP_ImageProcessing.PostProcessImageFromTexture(resultGridTexture);
    }

    /// <summary>
    /// Translates the pixel data from the texture to the boundary list
    /// </summary>
    /// <param name="texture">The input texture</param>
    private void PassBoundaryToList(Texture2D texture)
    {
        Vector2Int gridSize = new Vector2Int(Size.x, Size.z);
        Vector2Int textureSize = new Vector2Int(texture.width, texture.height);

        //List<Voxel> boundaryVoxels = new List<Voxel>();
        for (int x = 0; x < textureSize.x; x++)
        {
            for (int y = 0; y < textureSize.y; y++)
            //for (int y = textureSize.y - 1; y <= 0; y--)
            {
                var pixel = texture.GetPixel(x, y);
                if (pixel == Color.red)
                {
                    Boundaries.Add(Voxels[x, 0, y]);
                }
            }
        }
    }

    /// <summary>
    /// Clears the grid and populate a given amount of new configurable parts on the grid
    /// </summary>
    /// <param name="amt">The amount of parts to populate</param>
    /// <returns>The Texture that represents the grid state</returns>
    private Texture2D PopulateRandomConfigurableGetImage(int amt)
    {
        ClearGrid();
        var configurables = ExistingParts.OfType<ConfigurablePart>();
        foreach (var c in configurables) c.DestroyGO();
        ExistingParts = new List<Part>();
        for (int i = 0; i < amt; i++)
        {
            ConfigurablePart p = new ConfigurablePart(this, !_showVoxels, _popSeed);
            ExistingParts.Add(p);
        }
        //Write image to temp_sr folder
        return ImageReadWrite.TextureFromGrid(this);
    }

    /// <summary>
    /// Reads a structre file and creates the parts, feeding them into the _existingParts list
    /// </summary>
    /// <param name="file">The file to be read</param>
    private void ReadStructure(string file)
    {
        var newParts = JSONReader.ReadStructureAsList(this, file);
        foreach (var item in newParts)
        {
            ExistingParts.Add(item);
        }
    }

    /// <summary>
    /// Sets up the voxels array and faces for the constructor
    /// </summary>
    private void SetupVoxels()
    {
        Voxels = new Voxel[Size.x, Size.y, Size.z];

        for (int x = 0; x < Size.x; x++)
        {
            for (int y = 0; y < Size.y; y++)
            {
                for (int z = 0; z < Size.z; z++)
                {
                    Voxels[x, y, z] = new Voxel(new Vector3Int(x, y, z), this);
                }
            }
        }

        // make faces (from https://github.com/ADRC4/Voxel)
        Faces[0] = new Face[Size.x + 1, Size.y, Size.z];

        for (int x = 0; x < Size.x + 1; x++)
            for (int y = 0; y < Size.y; y++)
                for (int z = 0; z < Size.z; z++)
                {
                    Faces[0][x, y, z] = new Face(x, y, z, Axis.X, this);
                }

        Faces[1] = new Face[Size.x, Size.y + 1, Size.z];

        for (int x = 0; x < Size.x; x++)
            for (int y = 0; y < Size.y + 1; y++)
                for (int z = 0; z < Size.z; z++)
                {
                    Faces[1][x, y, z] = new Face(x, y, z, Axis.Y, this);
                }

        Faces[2] = new Face[Size.x, Size.y, Size.z + 1];

        for (int x = 0; x < Size.x; x++)
            for (int y = 0; y < Size.y; y++)
                for (int z = 0; z < Size.z + 1; z++)
                {
                    Faces[2][x, y, z] = new Face(x, y, z, Axis.Z, this);
                }
    }

}