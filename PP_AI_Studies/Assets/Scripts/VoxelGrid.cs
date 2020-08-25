using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System;

/// <summary>
/// The Grid / Slab Object that contains all the parts along with the geometry it represents
/// </summary>
public class VoxelGrid : MonoBehaviour
{
    #region Parameters and Fields

    #region Original fields and parameters (AVOID CHANGING) 
    public Vector3Int Size { get; private set; }
    public Voxel[,,] Voxels { get; private set; }
    public Face[][,,] Faces { get; private set; }
    public float VoxelSize { get; private set; }
    public Vector3 Origin { get; private set; }

    #endregion

    #region Migration Parameters

    //Grid setup
    //Currently available slabs: 44_44_A
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

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a rectangular VoxelGrid, with or without a slab GameObject
    /// </summary>
    /// <param name="size">The vector representing the size of the grid</param>
    /// <param name="voxelSize">The size of the voxel in metres</param>
    /// <param name="origin">The origin of the grid to be created</param>
    public VoxelGrid(Vector3Int size, float voxelSize, Vector3 origin, bool createGO = false, bool GOvisibility = false)
    {
        Size = size;
        VoxelSize = voxelSize;
        Origin = origin;
        Faces = new Face[3][,,];
        ExistingParts = new List<Part>();
        Spaces = new List<PPSpace>();
        Boundaries = new List<Voxel>();
        _showVoxels = !GOvisibility;
        if (Size == new Vector3Int(30, 1, 24))
        {
            _pix2pix = new PP_pix2pix("30x24");
        }
        else
        {
            _pix2pix = new PP_pix2pix("original");
        }
        
        SetupVoxels();

        if (createGO)
        {
            InstantiateGenericGO();
        }
    }

    /// <summary>
    /// Constructor for a  VoxelGrid instance from a file with its configurations
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

        _pix2pix = new PP_pix2pix("original");

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
    /// Constructor for a copy grid that holds only the states 
    /// </summary>
    /// <param name="original"></param>
    public VoxelGrid (VoxelGrid original)
    {
        Size = original.Size;
        Origin = original.Origin;
        
        Voxels = new Voxel[Size.x, Size.y, Size.z];
        for (int x = 0; x < Size.x; x++)
        {
            for (int y = 0; y < Size.y; y++)
            {
                for (int z = 0; z < Size.z; z++)
                {
                    Voxels[x, y, z] = original.Voxels[x,y,z].DeepCopyToGrid(this);
                }
            }
        }

        // make faces (from https://github.com/ADRC4/Voxel)
        Faces = new Face[3][,,];
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

        ExistingParts = new List<Part>();
        Spaces = new List<PPSpace>();
        Boundaries = new List<Voxel>();
        _pix2pix = original._pix2pix;
    }

    #endregion

    #region Original methods and Functions
    // AVOID CHANGING

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

    #endregion

    #region Migration methods and Functions

    /// <summary>
    /// Executes the internal Barracuda Pix2pix model, inferring from GPU and generating the respective spaces
    /// </summary>
    public void RunPopulationAndAnalysis()
    {
        Stopwatch aiStopwatch = new Stopwatch();
        aiStopwatch.Start();
        PopulateAndAnalyseGrid();
        Spaces = GenerateSpaces();
        aiStopwatch.Stop();
        var t = aiStopwatch.ElapsedMilliseconds;
        //_activityLog = $"AI Message: Generated {_spaces.Count} Spaces in {t} ms";
    }

    /// <summary>
    /// Executes a new analysis and generates new spaces
    /// </summary>
    public void RunAnalysisCreateNewSpaces()
    {
        //Stopwatch aiStopwatch = new Stopwatch();
        //aiStopwatch.Start();
        Boundaries = new List<Voxel>();
        var gridImage = GetStateImage();

        string folder = @"D:\GitRepo\PublicParts\PP_AI_Studies\temp_en\helpers\";

        var analysisResult = _pix2pix.GeneratePrediction(gridImage);
        //ImageReadWrite.SaveImage2Path(analysisResult, folder + "p2pOutput");

        var resultTexture = ProcessAnalysisResult(analysisResult);
        //ImageReadWrite.SaveImage2Path(resultTexture, folder + "postprosOutput");

        PassBoundaryToList(resultTexture);
        DestroySpaces();
        Spaces = GenerateSpaces();
        SetSpacesToConfigurableParts();
        //aiStopwatch.Stop();
        //var t = aiStopwatch.ElapsedMilliseconds;
    }

    /// <summary>
    /// Runs the Pix2pix analysis on the current state of the grid, while
    /// updating the spaces that can be understood as the same
    /// </summary>
    public void RunAnalysisUpdateSpaces()
    {
        //Stopwatch aiStopwatch = new Stopwatch();
        //aiStopwatch.Start();
        //Copy the existing spaces
        List<PPSpace> existingSpaces = new List<PPSpace>(Spaces);
        //Copy the existing indices
        List<HashSet<Vector3Int>> existingIndices = new List<HashSet<Vector3Int>>();
        foreach (var space in Spaces)
        {
            HashSet<Vector3Int> temp = new HashSet<Vector3Int>();
            foreach (var index in space.Indices)
            {
                temp.Add(new Vector3Int(index.x, index.y, index.z));
            }
            existingIndices.Add(temp);
        }

        //Generate new spaces
        Boundaries = new List<Voxel>();
        var gridImage = GetStateImage();
        var analysisResult = _pix2pix.GeneratePrediction(gridImage);
        var resultTexture = ProcessAnalysisResult(analysisResult);
        PassBoundaryToList(resultTexture);
        List<PPSpace> newSpaces = GenerateSpaces();
        List<PPSpace> resultSpaces = new List<PPSpace>();
        
        //Compare new spaces with existing spaces
        foreach (var nSpace in newSpaces)
        {
            PPSpace outSpace = nSpace;
            for (int i = 0; i < existingSpaces.Count; i++)
            {
                var eSpace = existingSpaces[i];
                var eIndices = existingIndices[i];

                if (nSpace.CompareSpaces(eSpace, eIndices))
                {
                    //The existing space parameters should be evaluated here
                    //nSpace.Name = "Existing";
                    nSpace.CopyDataFromSpace(eSpace);
                    //existingSpaces.Remove(eSpace);
                    break;
                }
            }
        }
        Spaces = newSpaces;
        SetSpacesToConfigurableParts();
        //aiStopwatch.Stop();
        //var t = aiStopwatch.ElapsedMilliseconds;
        //print($"Took {t} ms to update");
    }

    /// <summary>
    /// Goes through each ConfigurablePart and find its associated spaces
    /// </summary>
    private void SetSpacesToConfigurableParts()
    {
        foreach (var part in ExistingParts.OfType<ConfigurablePart>())
        {
            part.FindAssociatedSpaces();
        }
    }

    /// <summary>
    /// Generate spaces on the voxels that are not inside the parts boudaries, or space or part
    /// The method is inspired by a BFS algorithm, continuously checking the neighbours of the
    /// processed voxels until the minimum area is reached. It is designed to be called in a loop 
    /// that feeds the numbering / naming of the spaces
    /// </summary>
    /// <param name="number">Current number of the space</param>
    private PPSpace GenerateSingleSpace(int number, out bool result)
    {
        int maximumArea = 1000; //in voxel ammount
        var availableVoxels = ActiveVoxelsAsList().Where(v => !Boundaries.Contains(v) && !v.IsOccupied && !v.InSpace).ToList();
        //Initiate a new space
        PPSpace space = new PPSpace(this);
        result = true;
        if (availableVoxels.Count == 0)
        {
            result = false;
            return space;
        }
        Voxel originVoxel = availableVoxels[0];

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
        space.ValidadeSpace();
        return space;
        //Spaces.Add(space);
    }

    /// <summary>
    /// Generate spaces by looping <see cref="GenerateSingleSpace(int)"/> until all voxels are 
    /// assigned a space
    /// </summary>
    private List<PPSpace> GenerateSpaces()
    {
        //New spaces list
        List<PPSpace> newSpaces = new List<PPSpace>();
        DestroySpaces();
        //Clear spaces list
        //Spaces = new List<PPSpace>();

        int i = 0;
        //Generate spaces on vacant voxels inside boundaries
        while (ActiveVoxelsAsList().Any(v => !Boundaries.Contains(v) && !v.IsOccupied && !v.InSpace))
        {
            var space = GenerateSingleSpace(i++, out bool result);
            if (result)
            {
                newSpaces.Add(space);
            }
        }

        //Allocate boundary voxel to the smallest neighbouring space
        while (Boundaries.Any(b => !b.InSpace))
        {
            Voxels2SmallestNeighbour(Boundaries.Where(b => !b.InSpace));
        }
        foreach (var space in newSpaces)
        {
            if (space.IsSpare)
            {
                space.HideArrow();
            }
            
        }
        return newSpaces;
        //_activityLog = $"AI Message: Generated {Spaces.Count} Spaces";
    }

    /// <summary>
    /// Destroys the existing spaces and clears the existing Spaces list
    /// </summary>
    private void DestroySpaces()
    {
        foreach (var space in Spaces) space.DestroySpace();
        Spaces = new List<PPSpace>();
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
        GameObject reference = Resources.Load<GameObject>($"GameObjects/{_gridName}_{_gridType}_prefab");
        GridGO = Instantiate(reference);
        GridGO.transform.localPosition = Vector3.zero;
        GridGO.transform.localScale = new Vector3(VoxelSize, VoxelSize, VoxelSize);
        SetGOVisibility(!_showVoxels);
    }

    /// <summary>
    /// Creates a generic rectangular slab GameObject
    /// </summary>
    private void InstantiateGenericGO()
    {
        GameObject reference = Resources.Load<GameObject>($"GameObjects/GenericSlab");
        GridGO = Instantiate(reference);
        GridGO.transform.localScale = new Vector3(Size.x, Size.y, Size.z) * VoxelSize;
        GridGO.transform.localPosition = Origin + (GridGO.transform.localScale / 2f);
        SetGOVisibility(!_showVoxels);
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

        //string folder = @"D:\GitRepo\PublicParts\PP_AI_Studies\temp_en\helpers\";
        //ImageReadWrite.SaveImage2Path(analysisResult, folder + "p2pdownscaledOutput");

        //Create new texture with the same size as the original grid
        Texture2D resultGridTexture = new Texture2D(Size.x, Size.z);

        //Write result to texture
        for (int i = 0; i < resultGridTexture.width; i++)
        {
            for (int j = 0; j < resultGridTexture.height; j++)
            {
                int x = i;
                int y = analysisResult.height - resultGridTexture.height + j;
                resultGridTexture.SetPixel(i, j, analysisResult.GetPixel(x, y));
            }
        }
        resultGridTexture.Apply();
        //ImageReadWrite.SaveImage2Path(resultGridTexture, folder + "p2pcroppedOutput");

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
        return ImageReadWrite.TextureFromGridOriginal(this);
    }

    /// <summary>
    /// Gets the image from the grid
    /// </summary>
    /// <returns></returns>
    private Texture2D GetStateImage()
    {
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

    /// <summary>
    /// Sets the visibility of the child gameobjects of the GridGO
    /// </summary>
    /// <param name="visibility">The value to set</param>
    public void SetGOVisibility(bool visibility)
    {
        if (GridGO.transform.childCount > 0)
        {
            for (int i = 0; i < GridGO.transform.childCount; i++)
            {
                var mRenderer = GridGO.transform.GetChild(i).GetComponent<MeshRenderer>();
                mRenderer.enabled = visibility;
            }
        }

        else
        {
            var mRenderer = GridGO.transform.GetComponent<MeshRenderer>();
            mRenderer.enabled = visibility;
        }
    }

    /// <summary>
    /// Tries to get an existing space from a give Guid
    /// </summary>
    /// <param name="id">The Guid to be checked for</param>
    /// <returns>The <see cref="PPSpace"/> if it exists or null</returns>
    public PPSpace GetSpaceById (Guid id)
    {
        foreach (var space in Spaces)
        {
            if (space.SpaceId == id) return space;
        }

        return null;
    }

    /// <summary>
    /// Forces the list of spaces of a previous state onto the current one,
    /// only to be called after undoing an action
    /// </summary>
    /// <param name="previousSpaces"></param>
    public void ForceSpaceReset (List<PPSpace> previousSpaces)
    {
        Spaces = previousSpaces;
    }

    /// <summary>
    /// Method to creat a single <see cref="ConfigurablePart"/> on this grid in a given position with
    /// a given rotation and returns the ConfigurablePart object. The method attempts to create the component and the result parameter 
    /// represents the success of the operation
    /// </summary>
    /// <param name="origin">The ReferenceIndex to create the part on</param>
    /// <param name="rotation">The clockwise rotation to be applied to the part</param>
    /// <param name="name">The name to be given to the part</param>
    /// <param name="showVoxels">A boolean representing if the initial state should be with the voxels or GameObjects visible</param>
    /// <param name="result">The output boolean representing the result of the operation</param>
    /// <returns>The resulting <see cref="ConfigurablePart"/></returns>
    public ConfigurablePart CreateSinglePart(Vector3Int origin, int rotation, string name, bool showVoxels, out bool result)
    {
        ConfigurablePart p = new ConfigurablePart(this, origin, rotation, !showVoxels, name, out bool success);
        if (success)
        {
            ExistingParts.Add(p);
        }
        result = success;
        return p;
    }

    /// <summary>
    /// Restarts the grid in the end of a training episode
    /// </summary>
    public void RestartGrid()
    {
        //Should clear the grid, keeping only the existing parts
        foreach (Voxel voxel in Voxels)
        {
            voxel.IsOccupied = false;
            voxel.InSpace = false;
            voxel.ParentSpace = null;
            voxel.Part = null;
        }
        foreach (var space in Spaces)
        {
            space.DestroySpace();
        }
        Spaces = new List<PPSpace>();
        Boundaries = new List<Voxel>();

    }

    public void EnsurePartInGrid(ConfigurablePart part)
    {
        part.OccupyVoxels();
    }

    #endregion
}