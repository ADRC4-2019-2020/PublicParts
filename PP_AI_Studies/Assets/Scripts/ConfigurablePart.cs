using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Represents a Configurable Part in the VoxelGrid, its voxels and properties
/// </summary>
[System.Serializable]
public class ConfigurablePart : Part
{
    #region Properties and fields

    /// <summary>
    /// The GameObject that represents the configurable part
    /// </summary>
    public GameObject CPGameObject { get; private set; }
    public HashSet<PPSpace> AssociatedSpaces { get; private set; }
    public PP_Environment _environment { get; private set; }
    /// <summary>
    /// The configurable part agent
    /// </summary>
    public ConfigurablePartAgent CPAgent { get; private set; }
    public int Rotation { get; private set; }
    #endregion

    #region Constructors and helpers

    /// <summary>
    /// A generic empty constructor for the ConfigurablePart, placeholder for
    /// the <see cref="NewPart(VoxelGrid)"/> method, that should be deprecated
    /// </summary>
    public ConfigurablePart() { }

    /// <summary>
    /// Method for creating a new ConfigurablePart on a grid. Should be eventualy removed
    /// </summary>
    /// <param name="grid">The grid to create the ConfigurablePart on</param>
    /// <returns>The resulting ConfigurablePart</returns>
    public ConfigurablePart NewPart(VoxelGrid grid)
    {
        //This method does not need to exist, the constructor is enough
        //Check PPSpace and PPSpaceRequest Json reading methods when implementing new reading method
        //This is probably creating double the amount of spaces 
        ConfigurablePart p = new ConfigurablePart();
        p.Type = PartType.Configurable;
        p.Orientation = (PartOrientation)System.Enum.Parse(typeof(PartOrientation), OrientationName, false);
        p.Grid = grid;
        p.IsStatic = false;
        p.Height = Height;

        p.OCIndexes = OCIndexes;
        var indexes = p.OCIndexes.Split(';');
        p.nVoxels = indexes.Length;
        p.OccupiedIndexes = new Vector3Int[p.nVoxels];
        p.OccupiedVoxels = new Voxel[p.nVoxels];

        for (int i = 0; i < p.nVoxels; i++)
        {
            var index = indexes[i];
            var coords = index.Split('_');
            Vector3Int vector = new Vector3Int(int.Parse(coords[0]), int.Parse(coords[1]), int.Parse(coords[2]));
            var voxel = grid.Voxels[vector.x, vector.y, vector.z];
            voxel.IsOccupied = true;
            voxel.Part = p;
            p.OccupiedIndexes[i] = vector;
            p.OccupiedVoxels[i] = voxel;

        }
        p.ReferenceIndex = p.OccupiedIndexes[0];
        p.CalculateCenter();
        return p;
    }

    /// <summary>
    /// Constructor for a ConfigurablePart on the input grid in a randomized position
    /// and create its GameObject
    /// </summary>
    /// <param name="grid">The grid to create the ConfigurablePart on</param>
    /// <param name="goVisibility">Initial visibility of the GameObject</param>
    /// <param name="seed">Seed for the randomized creation</param>
    public ConfigurablePart (VoxelGrid grid, bool goVisibility, int seed)
    {
        //This constructor creates a random configurable part in the specified grid. 
        Type = PartType.Configurable;
        Grid = grid;
        _environment = Grid.GridGO.transform.parent.GetComponent<PP_Environment>();
        /*int minimumDistance = 6;*/ //In voxels
        Size = new Vector2Int(6, 2); //6 x 2 configurable part size SHOULD NOT BE HARD CODED
        nVoxels = Size.x * Size.y;
        OccupiedIndexes = new Vector3Int[nVoxels];
        IsStatic = false;
        Height = 6;
        Rotation = 0;
        //Random.InitState(seed);
        bool validPart = false;
        while (!validPart)
        {
            //Start from horizontal position
            //Orientation = (PartOrientation)Random.Range(0, 2);
            Orientation = PartOrientation.Horizontal;
            
            //Randomize ReferenceIndex
            int randomX = Random.Range(0, Grid.Size.x - 1);
            int randomY = Random.Range(0, Grid.Size.y - 1);
            int randomZ = Random.Range(0, Grid.Size.z - 1);
            ReferenceIndex = new Vector3Int(randomX, randomY, randomZ);
            SetPivot();

            GetOccupiedIndexes();
            bool allInside = true;

            //Set actual orientation and rotation
            Matrix4x4 rotationMatrix;
            Rotation = Random.Range(0, 4);
            if (Rotation == 0)
            {
                rotationMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, 0, 0));
            }
            else if (Rotation == 1)
            {
                //First rotation, 90 degrees clockwise
                Orientation = PartOrientation.Vertical;
                rotationMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, 90f, 0));
            }
            else if (Rotation == 2)
            {
                //Second rotation, 180 degrees clockwise
                rotationMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, 180f, 0));
            }
            else
            {
                //Second rotation, 270 degrees clockwise
                Orientation = PartOrientation.Vertical;
                rotationMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, 270f, 0));
            }

            //Apply rotation
            for (int i = 0; i < OccupiedIndexes.Length; i++)
            {
                //Rotate index
                var existingIndex = new Vector3Int(OccupiedIndexes[i].x, OccupiedIndexes[i].y, OccupiedIndexes[i].z);
                Vector3 rotatedIndex = rotationMatrix.MultiplyPoint(existingIndex - PartPivot) + PartPivot;

                //Resulting coordinates
                int x = Mathf.RoundToInt(rotatedIndex.x);
                int y = Mathf.RoundToInt(rotatedIndex.y);
                int z = Mathf.RoundToInt(rotatedIndex.z);

                OccupiedIndexes[i] = new Vector3Int(x, y, z);
            }

            //Validate part on grid
            foreach (var index in OccupiedIndexes)
            {
                if (index.x >= Grid.Size.x || index.x < 0 || index.y >= Grid.Size.y || index.y < 0 || index.z >= Grid.Size.z || index.z < 0)
                {
                    allInside = false;
                    break;
                }
                else if (Grid.Voxels[index.x, index.y, index.z].IsOccupied || !Grid.Voxels[index.x, index.y, index.z].IsActive)
                {
                    allInside = false;
                    break;
                }
            }

            //Validate based on existing parts
            if (allInside)
            {
                if (!CheckValidDistance()) continue;
                else validPart = true;
            }
        }
        OccupyVoxels();
        CreateGameObject(Rotation);
        
        SetGOVisibility(goVisibility);
    }

    /// <summary>
    /// Constructor for a <see cref="ConfigurablePart"/> on the input grid in a randomized position
    /// and create its <see cref="GameObject"/>, trying once and returning its result success
    /// </summary>
    /// <param name="grid">The <see cref="VoxelGrid"/> to create the <see cref="ConfigurablePart"/> on</param>
    /// <param name="goVisibility">The initial visibility of the <see cref="GameObject"/></param>
    /// <param name="seed">The seed value for the randomized creation</param>
    /// <param name="name">The name of the new <see cref="ConfigurablePart"/></param>
    /// <param name="success">The result of the operation</param>
    public ConfigurablePart(VoxelGrid grid, bool goVisibility, int seed, string name, out bool success)
    {
        Type = PartType.Configurable;
        Grid = grid;
        _environment = Grid.GridGO.transform.parent.GetComponent<PP_Environment>();
        Size = new Vector2Int(6, 2);
        nVoxels = Size.x * Size.y;
        OccupiedIndexes = new Vector3Int[nVoxels];
        IsStatic = false;
        Height = 6;
        Name = name;
        Random.InitState(seed);

        success = false;

        //Start from horizontal position
        Orientation = PartOrientation.Horizontal;
        
        //Randomize ReferenceIndex
        int randomX = Random.Range(0, Grid.Size.x - 1);
        int randomY = Random.Range(0, Grid.Size.y - 1);
        int randomZ = Random.Range(0, Grid.Size.z - 1);
        ReferenceIndex = new Vector3Int(randomX, randomY, randomZ);

        //Prepare the pivot and occupied indexes
        SetPivot();
        GetOccupiedIndexes();

        //Randomize the rotation
        Rotation = Random.Range(0, 4);

        //Change the orientation if rotation makes part vertical
        if (Rotation == 1 || Rotation == 3) Orientation = PartOrientation.Vertical;

        //Define the rotation matrix
        Matrix4x4 rotationMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, Rotation * 90f, 0));

        //Apply rotation if rotation > 0
        if (Rotation > 0)
        {
            for (int i = 0; i < OccupiedIndexes.Length; i++)
            {
                //Rotate index
                var existingIndex = new Vector3Int(OccupiedIndexes[i].x, OccupiedIndexes[i].y, OccupiedIndexes[i].z);
                Vector3 rotatedIndex = rotationMatrix.MultiplyPoint(existingIndex - PartPivot) + PartPivot;

                //Resulting coordinates
                int x = Mathf.RoundToInt(rotatedIndex.x);
                int y = Mathf.RoundToInt(rotatedIndex.y);
                int z = Mathf.RoundToInt(rotatedIndex.z);

                OccupiedIndexes[i] = new Vector3Int(x, y, z);
            }
        }
        

        //Validate part on grid
        foreach (var index in OccupiedIndexes)
        {
            if (index.x >= Grid.Size.x || index.x < 0 || index.y >= Grid.Size.y || index.y < 0 || index.z >= Grid.Size.z || index.z < 0)
            {
                return;
            }
            else if (Grid.Voxels[index.x, index.y, index.z].IsOccupied || !Grid.Voxels[index.x, index.y, index.z].IsActive)
            {
                return;
            }
        }

        //Validate based on existing parts
        if (!CheckValidDistance()) return;

        //Set creation as successful and create the part on grid and create GO
        success = true;
        OccupyVoxels();
        CreateGameObject(Rotation);
        SetGOVisibility(goVisibility);
    }

    /// <summary>
    /// Creates a ConfigurablePart in the specified index, with the desired rotation. 
    /// If it tries to create a part in an invalid position, the output boolean will be false and
    /// neither the GameObject will be created nor the grid will be modified.
    /// </summary>
    /// <param name="grid">The <see cref="VoxelGrid"/> to create the <see cref="ConfigurablePart"/> in.</param>
    /// <param name="originIndex">The origin index on the <see cref="VoxelGrid"/></param>
    /// <param name="rotation">The rotation to apply to the <see cref="ConfigurablePart"/>, multiplied by 90</param>
    /// <param name="goVisibility">The initial visibility state of the <see cref="GameObject"/></param>
    /// <param name="success">The output representing the success of the operation</param>
    public ConfigurablePart(VoxelGrid grid, Vector3Int originIndex , int rotation, bool goVisibility, string name, out bool success)
    {
        Type = PartType.Configurable;
        Grid = grid;
        Name = name;
        _environment = Grid.GridGO.transform.parent.GetComponent<PP_Environment>();
        Size = new Vector2Int(6, 2); //6 x 2 configurable part size SHOULD NOT BE HARD CODED
        nVoxels = Size.x * Size.y;
        OccupiedIndexes = new Vector3Int[nVoxels];
        IsStatic = false;
        Height = 6;
        success = false;
        Rotation = rotation;
        //Start from horizontal position
        Orientation = PartOrientation.Horizontal;
        ReferenceIndex = originIndex;
        
        SetPivot();
        GetOccupiedIndexes();

        //Set actual orientation and rotation
        Matrix4x4 rotationMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, Rotation * 90f, 0));

        //Apply rotation if rotation > 0
        if (Rotation > 0)
        {
            for (int i = 0; i < OccupiedIndexes.Length; i++)
            {
                //Rotate index
                var existingIndex = new Vector3Int(OccupiedIndexes[i].x, OccupiedIndexes[i].y, OccupiedIndexes[i].z);
                Vector3 rotatedIndex = rotationMatrix.MultiplyPoint(existingIndex - PartPivot) + PartPivot;

                //Resulting coordinates
                int x = Mathf.RoundToInt(rotatedIndex.x);
                int y = Mathf.RoundToInt(rotatedIndex.y);
                int z = Mathf.RoundToInt(rotatedIndex.z);

                OccupiedIndexes[i] = new Vector3Int(x, y, z);
            }
        }

        //Validate part on grid
        foreach (var index in OccupiedIndexes)
        {
            if (index.x >= Grid.Size.x || index.x < 0 || index.y >= Grid.Size.y || index.y < 0 || index.z >= Grid.Size.z || index.z < 0)
            {
                return;
            }
            else if (Grid.Voxels[index.x, index.y, index.z].IsOccupied || !Grid.Voxels[index.x, index.y, index.z].IsActive)
            {
                return;
            }
        }

        //Set creation as successful and create the part on grid and create GO
        success = true;
        OccupyVoxels();
        CreateGameObject(rotation);
        SetGOVisibility(goVisibility);
        //Name = $"CP_{ReferenceIndex.x}_{ReferenceIndex.x}_{ReferenceIndex.x}";
    }

    /// <summary>
    /// Constructor for a <see cref="ConfigurablePart"/> placed in a blank position
    /// </summary>
    /// <param name="grid">The grid to place the part in</param>
    /// <param name="goVisibility">The initial visibility of the <see cref="GameObject"/></param>
    /// <param name="name">The name of the <see cref="ConfigurablePart"/></param>
    public ConfigurablePart(VoxelGrid grid, bool goVisibility, string name)
    {
        //Iniitialize the properties as defaults
        Type = PartType.Configurable;
        Grid = grid;
        _environment = Grid.GridGO.transform.parent.GetComponent<PP_Environment>();
        Size = new Vector2Int(6, 2);
        nVoxels = Size.x * Size.y;
        OccupiedIndexes = new Vector3Int[nVoxels];
        OccupiedVoxels = new Voxel[nVoxels];
        IsStatic = false;
        Height = 6;
        Name = name;
        Orientation = PartOrientation.Horizontal;
        Rotation = 0;
        ReferenceIndex = Vector3Int.zero;

        

        //Create the game object and the agent
        CreateGameObject(Rotation);
        SetGOVisibility(goVisibility);
    }

    #endregion

    #region Voxel and grid Methods

    /// <summary>
    /// Sets the value of the PartPivot of the Configurable Part
    /// </summary>
    private void SetPivot()
    {
        PartPivot = new Vector3(ReferenceIndex.x - 0.5f, ReferenceIndex.y, ReferenceIndex.z - 0.5f);
    }

    /// <summary>
    /// Checks if the proposed position for a new ConfigurablePart is valid on its parent Grid.
    /// Utilized on <see cref="ConfigurablePart()"/>
    /// </summary>
    /// <returns>Boolean representing the validity</returns>
    private bool CheckValidDistance()
    {
        //Set the search sides according to index parity, even = negative, odd = positive
        Vector3Int[] evenSide = OccupiedIndexes.Where((x, i) => i % 2 == 0).ToArray();
        Vector3Int[] oddSide = OccupiedIndexes.Where((x, i) => i % 2 != 0).ToArray();

        //Set the search ranges according to part orientation, for boundary and for parallel parts and agnostic parts
        int boundaryRange = 4;
        int partsRange = 6;

        //Check, for both sides and both ranges if there is any impediment
        if (Orientation == PartOrientation.Horizontal)
        {
            //Try checking on both, starting from the even side, avoiding duplicates
            int[] pRange = new int[] { evenSide[0].z - partsRange, evenSide[0].z + partsRange + 1 };
            int[] bRange = new int[] { evenSide[0].z - boundaryRange - 1, evenSide[0].z + boundaryRange + 2 };
            foreach (var index in evenSide)
            {
                for (int z = pRange[0]; z <= pRange[1]; z++)
                {
                    //Skip its own indexes
                    if (z == index.z || z == index.z + 1) continue;
                    
                    //Return false if outside the grid
                    if (z < 0 || z > Grid.Size.z - 1) return false;
                    
                    Voxel checkVoxel = Grid.Voxels[index.x, index.y, z];
                    
                    //Return false if, within boundary search range, the voxel is not active (avoid perpendicular boundary)
                    if (z >= bRange[0] && z <= bRange[1])
                    {
                        if (!checkVoxel.IsActive) return false;
                    }

                    //Return false if a part is found and is not perpendicular
                    if (checkVoxel.IsOccupied && checkVoxel.Part.Orientation != PartOrientation.Vertical)
                    {
                        return false;
                    }
                }
            }
        }

        if (Orientation == PartOrientation.Vertical)
        {
            //Try checking on both, starting from the even side, avoiding duplicates
            int[] pRange = new int[] { evenSide[0].x - partsRange, evenSide[0].x + partsRange + 1 };
            int[] bRange = new int[] { evenSide[0].x - boundaryRange - 1, evenSide[0].x + boundaryRange + 2 };
            
            foreach (var index in evenSide)
            {
                for (int x = pRange[0]; x <= pRange[1]; x++)
                {
                    //Skip its own indexes
                    if (x == index.x || x == index.x + 1) continue;
                    
                    //Return false if outside the grid
                    if (x < 0 || x > Grid.Size.x - 1) return false;
                    
                    Voxel checkVoxel = Grid.Voxels[x, index.y, index.z];

                    //Return false if, within boundary search range, the voxel is not active (avoid perpendicular boundary)
                    if (x >= bRange[0] && x <= bRange[1])
                    {
                        if (!checkVoxel.IsActive) return false;
                    }

                    //Return false if a part is found and is not perpendicular
                    if (checkVoxel.IsOccupied && checkVoxel.Part.Orientation != PartOrientation.Horizontal)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Populate the OccupiedIndexes array, a voxel based representation of the
    /// position of the ConfigurablePart
    /// </summary>
    private void GetOccupiedIndexes()
    {
        //This can be refactored
        //Check orientation and switch Size.x and Size.y as 
        //Longitudinal and transversal accordingly
        if (Orientation == PartOrientation.Horizontal)
        {
            int i = 0;
            for (int x = 0; x < Size.x; x++)
            {
                for (int z = 0; z < Size.y; z++)
                {
                    OccupiedIndexes[i++] = new Vector3Int(ReferenceIndex.x + x, ReferenceIndex.y, ReferenceIndex.z + z);
                }
            }

        }
        else if (Orientation == PartOrientation.Vertical)
        {
            int i = 0;
            for (int x = 0; x < Size.y; x++)
            {
                for (int z = 0; z < Size.x; z++)
                {
                    OccupiedIndexes[i++] = new Vector3Int(ReferenceIndex.x + x, ReferenceIndex.y, ReferenceIndex.z + z);
                }
            }
        }
    }

    /// <summary>
    /// Finds the spaces associated to this <see cref="ConfigurablePart"/> and
    /// populates the <see cref="AssociatedSpaces"/> set
    /// </summary>
    public void FindAssociatedSpaces()
    {
        AssociatedSpaces = new HashSet<PPSpace>();
        foreach (var voxel in OccupiedVoxels)
        {
            var neighbours = voxel.GetFaceNeighbours().Where(n => n.InSpace);
            foreach (var neighbour in neighbours)
            {
                var s = neighbour.ParentSpace;
                if (!AssociatedSpaces.Contains(s))
                {
                    AssociatedSpaces.Add(s);
                }
            }
        }
    }

    /// <summary>
    /// Finds a new position for the <see cref="ConfigurablePart"/>, avoiding its destruction
    /// </summary>
    /// <param name="seed">The seed for the Random method</param>
    /// <param name="success">The result of the operation</param>
    public void FindNewPosition(int seed, out bool success)
    {
        Random.InitState(seed);
        //Clean the voxels, just to be safe
        //foreach (Voxel voxel in OccupiedVoxels)
        //{
        //    if (voxel != null)
        //    {
        //        voxel.IsOccupied = false;
        //        voxel.Part = null;
        //    }
        //}

        //OccupiedVoxels = new Voxel[nVoxels];
        //OccupiedIndexes = new Vector3Int[nVoxels];

        success = false;

        //Start from horizontal position
        Orientation = PartOrientation.Horizontal;
        Rotation = 0;

        //Randomize ReferenceIndex
        int randomX = Random.Range(0, Grid.Size.x - 1);
        int randomY = Random.Range(0, Grid.Size.y - 1);
        int randomZ = Random.Range(0, Grid.Size.z - 1);
        ReferenceIndex = new Vector3Int(randomX, randomY, randomZ);

        //Prepare the pivot and occupied indexes
        SetPivot();
        GetOccupiedIndexes();

        //Randomize the rotation
        Rotation = Random.Range(0, 4);

        //Change the orientation if rotation makes part vertical
        if (Rotation == 1 || Rotation == 3) Orientation = PartOrientation.Vertical;

        //Apply rotation if rotation > 0
        if (Rotation > 0)
        {
            //Define the rotation matrix
            Matrix4x4 rotationMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, Rotation * 90f, 0));
            for (int i = 0; i < OccupiedIndexes.Length; i++)
            {
                //Rotate index
                var existingIndex = new Vector3Int(OccupiedIndexes[i].x, OccupiedIndexes[i].y, OccupiedIndexes[i].z);
                Vector3 rotatedIndex = rotationMatrix.MultiplyPoint(existingIndex - PartPivot) + PartPivot;

                //Resulting coordinates
                int x = Mathf.RoundToInt(rotatedIndex.x);
                int y = Mathf.RoundToInt(rotatedIndex.y);
                int z = Mathf.RoundToInt(rotatedIndex.z);

                OccupiedIndexes[i] = new Vector3Int(x, y, z);
            }
        }


        //Validate part on grid
        foreach (var index in OccupiedIndexes)
        {
            if (index.x >= Grid.Size.x || index.x < 0 || index.y >= Grid.Size.y || index.y < 0 || index.z >= Grid.Size.z || index.z < 0)
            {
                return;
            }
            else if (Grid.Voxels[index.x, index.y, index.z].IsOccupied || !Grid.Voxels[index.x, index.y, index.z].IsActive)
            {
                return;
            }
        }

        //Validate based on existing parts
        if (!CheckValidDistance()) return;

        //Set creation as successful and create the part on grid and create GO
        OccupyVoxels();
        SetGOTransformations();
        success = true;
    }

    public void JumpToNewPosition(int seed, out bool success)
    {
        foreach (Voxel voxel in OccupiedVoxels)
        {
            if (voxel != null)
            {
                voxel.IsOccupied = false;
                voxel.Part = null;
                var index = voxel.Index;
                var onGrid = Grid.Voxels[index.x, index.y, index.z];
                onGrid.IsOccupied = false;
                onGrid.Part = null;
            }
        }

        OccupiedVoxels = new Voxel[nVoxels];
        OccupiedIndexes = new Vector3Int[nVoxels];
        //success = true;
        FindNewPosition(seed, out success);
    }

    /// <summary>
    /// Properly resets the position data of the part befor finding a new position on the grid
    /// </summary>
    public void ResetPosition()
    {
        foreach (Voxel voxel in OccupiedVoxels)
        {
            if (voxel != null)
            {
                voxel.IsOccupied = false;
                voxel.Part = null;
            }
        }

        OccupiedVoxels = new Voxel[nVoxels];
        OccupiedIndexes = new Vector3Int[nVoxels];
    }

    #endregion

    #region GameObject Methods

    /// <summary>
    /// Creates the GameObject of the ConfigurablePart
    /// </summary>
    private void CreateGameObject(int rotation)
    {
        var voxelSize = Grid.VoxelSize;
        GameObject reference = Resources.Load<GameObject>("GameObjects/ConfigurableComponentAgent");
        CPGameObject = GameObject.Instantiate(reference, Grid.GridGO.transform.parent);
        CPGameObject.transform.name = Name;
        SetGOTransformations();

        CPAgent = CPGameObject.GetComponent<ConfigurablePartAgent>();
        CPAgent.SetPart(this);
    }

    /// <summary>
    /// Sets the Transform of the <see cref="ConfigurablePart"/> GameObject that has been initialized
    /// </summary>
    /// <param name="rotation">The rotation set to the part</param>
    private void SetGOTransformations()
    {
        var voxelSize = Grid.VoxelSize;
        var xPos = ReferenceIndex.x;
        var yPos = ReferenceIndex.y + 1;
        var zPos = ReferenceIndex.z;

        CPGameObject.transform.localPosition = new Vector3(xPos, yPos, zPos) * voxelSize;
        CPGameObject.transform.localScale = new Vector3(voxelSize, voxelSize, voxelSize);
        CPGameObject.transform.localRotation = Quaternion.Euler(0, Rotation * 90f, 0);
    }
    
    /// <summary>
    /// Assigns the position of the voxels to GameObject
    /// </summary>
    private void SetGOPosition()
    {
        var voxelSize = Grid.VoxelSize;
        CPGameObject.transform.localPosition = new Vector3(ReferenceIndex.x, ReferenceIndex.y + 1, ReferenceIndex.z) * voxelSize;

        if (Orientation == PartOrientation.Vertical)
        {
            CPGameObject.transform.rotation = Quaternion.Euler(0, 90, 0);
            CPGameObject.transform.position += new Vector3(0, 0, 6 * voxelSize);
        }

        if (Random.Range(0, 2) == 1)
        {
            CPGameObject.transform.rotation = Quaternion.Euler(0, 180, 0);
            if (Orientation == PartOrientation.Horizontal)
            {
                CPGameObject.transform.rotation = Quaternion.Euler(0, 180, 0);
                CPGameObject.transform.position += new Vector3(6 * voxelSize, 0, 2 * voxelSize);
            }
            else
            {
                CPGameObject.transform.rotation = Quaternion.Euler(0, 270, 0);
                CPGameObject.transform.position += new Vector3(2 * voxelSize, 0, -6 * voxelSize);
            }

        }
    }

    /// <summary>
    /// Destroys the GameObject that represents this ConfigurablePart
    /// </summary>
    public void DestroyGO()
    {
        CPGameObject.GetComponent<ConfigurablePartAgent>().SelfDestroy();
        CPGameObject = null;
    }

    /// <summary>
    /// Modifies the visibility state of the GameObject
    /// </summary>
    /// <param name="visible">Boolean to set</param>
    public void SetGOVisibility(bool visible)
    {
        //CPGameObject.SetActive(visible);
        CPAgent.SetVisibility(visible);
    }

    #endregion

    #region Movement methods

    /// <summary>
    /// Tries to move the ConfigurablePart object in the X direction on the Grid.
    /// Calls an update of the <see cref="PPSpace"/>s on the <see cref="VoxelGrid"/>
    /// on every execution.
    /// </summary>
    /// <param name="distance">The distance to be moved</param>
    /// <param name="updateSpaces">Bool to trigger space evaluation update. Default is true</param>
    /// <param name="freezeAgent">Bool to trigger the freezing of the agent once done with movement. Default is true</param>
    /// <returns>The boolean representing if movement was successful</returns>
    public bool MoveInX(int distance, bool updateSpaces = true, bool freezeAgent = true)
    {
        //Boolean to evalute the validity of the movement
        bool validMovement = true;
        Vector3Int[] tempIndexes = new Vector3Int[OccupiedIndexes.Length];
        if (distance == 1 || distance == -1)
        {
            //Temporarily store the position of the new indexes
            //While checking their validity
            
            for (int i = 0; i < OccupiedIndexes.Length; i++)
            {
                int x = OccupiedIndexes[i].x + distance;
                // If new x position is beyond grid, return false
                if (x < 0 || x > Grid.Size.x -1) return false;

                int y = OccupiedIndexes[i].y;
                int z = OccupiedIndexes[i].z;
                var voxel = Grid.Voxels[x, y, z];
                
                //If the movement includes a voxel that is not active or
                //is occupied by a part that isn't this, return false
                if (!voxel.IsActive || (voxel.IsOccupied && voxel.Part != this))
                {
                    validMovement = false;
                    return validMovement;
                }
                //If everything is ok, add new index to temporary array
                tempIndexes[i] = new Vector3Int(x, y, z);
            }
        }
        else
        {
            //If distance is not +1 or -1, return false
            validMovement = false;
            return validMovement;
        }
        
        //Apply movement to grid
        for (int i = 0; i < OccupiedIndexes.Length; i++)
        {
            var preVoxel = Grid.Voxels[OccupiedIndexes[i].x, OccupiedIndexes[i].y, OccupiedIndexes[i].z];
            preVoxel.IsOccupied = false;
            preVoxel.Part = null;

        }

        for (int i = 0; i < OccupiedIndexes.Length; i++)
        {
            var newIndex = tempIndexes[i];
            var newVoxel = Grid.Voxels[newIndex.x, newIndex.y, newIndex.z];
            newVoxel.IsOccupied = true;
            newVoxel.Part = this;

            OccupiedIndexes[i] = newIndex;
        }
        //Move reference index and Pivot
        ReferenceIndex += new Vector3Int(distance, 0, 0);
        SetPivot();

        if (freezeAgent) CPAgent.FreezeAgent();
        //Call to Update the slab in the environment
        if (updateSpaces) _environment.AnalyzeGridUpdateSpaces();

        return validMovement;
    }

    /// <summary>
    /// Tries to move the ConfigurablePart object in the X direction on the Grid
    /// Calls an update of the <see cref="PPSpace"/>s on the <see cref="VoxelGrid"/>
    /// on every execution.
    /// </summary>
    /// <param name="distance">The distance to be moved</param>
    /// <param name="updateSpaces">Bool to trigger space evaluation update. Default is true</param>
    /// <param name="freezeAgent">Bool to trigger the freezing of the agent once done with movement. Default is true</param>
    /// <returns>The boolean representing if movement was successful</returns>
    public bool MoveInZ(int distance, bool updateSpaces = true, bool freezeAgent = true)
    {
        ////Boolean to evalute the validity of the movement
        //bool validMovement = true;
        //Vector3Int[] tempIndexes = new Vector3Int[OccupiedIndexes.Length];
        //if (distance == 1 || distance == -1)
        //{
        //    //Temporarily store the position of the new indexes
        //    //While checking their validity

        //    for (int i = 0; i < OccupiedIndexes.Length; i++)
        //    {
        //        int x = OccupiedIndexes[i].x;
        //        int y = OccupiedIndexes[i].y;

        //        int z = OccupiedIndexes[i].z + distance;
        //        // If new z position is beyond grid, return false
        //        if (z < 0 || z > Grid.Size.z -1) return false;
        //        var voxel = Grid.Voxels[x, y, z];

        //        //If the movement includes a voxel that is not active or
        //        //is occupied by a part that isn't this, return false
        //        if (!voxel.IsActive || (voxel.IsOccupied && voxel.Part != this))
        //        {
        //            validMovement = false;
        //            return validMovement;
        //        }
        //        //If everything is ok, add new index to temporary array
        //        tempIndexes[i] = new Vector3Int(x, y, z);
        //    }
        //}
        //else
        //{
        //    //If distance is not +1 or -1, return false
        //    validMovement = false;
        //    return validMovement;
        //}

        ////Apply movement to grid
        //for (int i = 0; i < OccupiedIndexes.Length; i++)
        //{
        //    var preVoxel = Grid.Voxels[OccupiedIndexes[i].x, OccupiedIndexes[i].y, OccupiedIndexes[i].z];
        //    preVoxel.IsOccupied = false;
        //    preVoxel.Part = null;

        //}

        //for (int i = 0; i < OccupiedIndexes.Length; i++)
        //{
        //    var newIndex = tempIndexes[i];
        //    var newVoxel = Grid.Voxels[newIndex.x, newIndex.y, newIndex.z];
        //    newVoxel.IsOccupied = true;
        //    newVoxel.Part = this;

        //    OccupiedIndexes[i] = newIndex;
        //}

        ////Move reference index and Pivot
        //ReferenceIndex += new Vector3Int(0, 0, distance);
        //SetPivot();

        //if (freezeAgent) CPAgent.FreezeAgent();
        ////Call to Update the slab in the environment
        //if (updateSpaces)
        //{
        //    _environment.AnalyzeGridUpdateSpaces();
        //}

        //return validMovement;

        //Boolean to evalute the validity of the movement
        bool validMovement = true;
        Vector3Int[] tempIndexes = new Vector3Int[OccupiedIndexes.Length];
        if (distance == 1 || distance == -1)
        {
            //Temporarily store the position of the new indexes
            //While checking their validity

            for (int i = 0; i < OccupiedIndexes.Length; i++)
            {
                int x = OccupiedIndexes[i].x;
                int y = OccupiedIndexes[i].y;

                int z = OccupiedIndexes[i].z + distance;
                // If new x position is beyond grid, return false
                if (z < 0 || z > Grid.Size.z - 1) return false;

                var voxel = Grid.Voxels[x, y, z];

                //If the movement includes a voxel that is not active or
                //is occupied by a part that isn't this, return false
                if (!voxel.IsActive || (voxel.IsOccupied && voxel.Part != this))
                {
                    validMovement = false;
                    return validMovement;
                }
                //If everything is ok, add new index to temporary array
                tempIndexes[i] = new Vector3Int(x, y, z);
            }
        }
        else
        {
            //If distance is not +1 or -1, return false
            validMovement = false;
            return validMovement;
        }

        //Apply movement to grid
        for (int i = 0; i < OccupiedIndexes.Length; i++)
        {
            var preVoxel = Grid.Voxels[OccupiedIndexes[i].x, OccupiedIndexes[i].y, OccupiedIndexes[i].z];
            preVoxel.IsOccupied = false;
            preVoxel.Part = null;

        }

        for (int i = 0; i < OccupiedIndexes.Length; i++)
        {
            var newIndex = tempIndexes[i];
            var newVoxel = Grid.Voxels[newIndex.x, newIndex.y, newIndex.z];
            newVoxel.IsOccupied = true;
            newVoxel.Part = this;

            OccupiedIndexes[i] = newIndex;
        }
        //Move reference index and Pivot
        ReferenceIndex += new Vector3Int(0, 0, distance);
        SetPivot();

        if (freezeAgent) CPAgent.FreezeAgent();
        //Call to Update the slab in the environment
        if (updateSpaces) _environment.AnalyzeGridUpdateSpaces();

        return validMovement;
    }

    /// <summary>
    /// Tries to rotate the component around its ReferenceIndex.
    /// A direction of +1 rotetes clockwise and -1 anticlockwise.
    /// Calls an update of the <see cref="PPSpace"/>s on the <see cref="VoxelGrid"/>
    /// on every execution.
    /// </summary>
    /// <param name="direction">The direction to rotate </param>
    /// <param name="updateSpaces">Bool to trigger space evaluation update. Default is true</param>
    /// <param name="freezeAgent">Bool to trigger the freezing of the agent once done with movement. Default is true</param>
    /// <returns>The boolean representing if the rotation was successful</returns>
    public bool RotateComponent(int direction, bool updateSpaces = true, bool freezeAgent = true)
    {
        //Boolean to evalute the validity of the rotation
        bool validRotation = true;
        
        //New orientation
        PartOrientation newOrientation = PartOrientation.Horizontal;
        if (Orientation == PartOrientation.Horizontal) newOrientation = PartOrientation.Vertical;
        
        //Temporary store new indexes
        Vector3Int[] tempIndexes = new Vector3Int[OccupiedIndexes.Length];
        if (direction == 1 || direction == -1)
        {
            //Define the rotation matrix
            Matrix4x4 rotationMatrix;
            if (direction == 1)
            {
                rotationMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, 90f, 0));
            }
            else
            {
                rotationMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, -90f, 0));
            }
            
            for (int i = 0; i < OccupiedIndexes.Length; i++)
            {
                //Rotate index
                var existingIndex = new Vector3Int(OccupiedIndexes[i].x, OccupiedIndexes[i].y, OccupiedIndexes[i].z);
                Vector3 rotatedIndex = rotationMatrix.MultiplyPoint(existingIndex - PartPivot) + PartPivot;
                
                //Resulting coordinates
                int x = Mathf.RoundToInt(rotatedIndex.x);
                int y = Mathf.RoundToInt(rotatedIndex.y);
                int z = Mathf.RoundToInt(rotatedIndex.z);

                // If new x position is beyond grid, return false
                if (x < 0 || x > Grid.Size.x - 1) return false;

                // If new z position is beyond grid, return false
                if (z < 0 || z > Grid.Size.z - 1) return false;
                var voxel = Grid.Voxels[x, y, z];

                //If the movement includes a voxel that is not active or
                //is occupied by a part that isn't this, return false
                if (!voxel.IsActive || (voxel.IsOccupied && voxel.Part != this))
                {
                    validRotation = false;
                    return validRotation;
                }
                //If everything is ok, add new index to temporary array
                tempIndexes[i] = new Vector3Int(x, y, z);
            }
        }
        else
        {
            validRotation = false;
            return validRotation;
        }

        //Apply Flipping to grid
        for (int i = 0; i < OccupiedIndexes.Length; i++)
        {
            var preVoxel = Grid.Voxels[OccupiedIndexes[i].x, OccupiedIndexes[i].y, OccupiedIndexes[i].z];
            preVoxel.IsOccupied = false;
            preVoxel.Part = null;

        }

        for (int i = 0; i < OccupiedIndexes.Length; i++)
        {
            var newIndex = tempIndexes[i];
            var newVoxel = Grid.Voxels[newIndex.x, newIndex.y, newIndex.z];
            newVoxel.IsOccupied = true;
            newVoxel.Part = this;

            OccupiedIndexes[i] = newIndex;
        }
        Orientation = newOrientation;
        
        Rotation = Rotation + direction;
        if (Rotation == 4)
        {
            Rotation = 0;
        }
        else if (Rotation < 0)
        {
            Rotation = 3;
        }

        if (freezeAgent) CPAgent.FreezeAgent();
        //Call to Update the slab in the environment
        if (updateSpaces)
        {
            _environment.AnalyzeGridUpdateSpaces();
        }
        return validRotation;
    }

    #endregion
}
