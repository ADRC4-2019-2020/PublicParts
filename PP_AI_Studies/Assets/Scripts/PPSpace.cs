using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Globalization;

[System.Serializable]
public class PPSpace : IEquatable<PPSpace>
{ 
    //
    //FIELDS AND PROPERTIES
    //
    private VoxelGrid _grid;
    public HashSet<Voxel> Voxels = new HashSet<Voxel>();
    public HashSet<Vector3Int> Indices = new HashSet<Vector3Int>();
    public string OCIndexes; // Used to read Space data from Json file
    public string Name;

    public bool Occupied;
    private Tenant _occupyingTenant;
    private PPSpaceRequest _usedRequest;
    private int _durationLeft;

    /// <summary>
    /// The average center of this space, used to create the data exposer
    /// </summary>
    Vector3 _center => new Vector3(
        Indices.Average(i => (float)i.x),
        0,
        Indices.Average(i => (float)i.z)) * _grid.VoxelSize;

    //Game object used to visualize space data
    GameObject _infoArrow;

    /// <summary>
    /// Boudary voxels are voxels which have at least one face neighbour which isn't part of its ParentSpace
    /// </summary>
    public IEnumerable<Voxel> BoundaryVoxels => Voxels.Where(v =>
        v.GetFaceNeighbours()
        .Any(n => !Voxels.Contains(n))
        || v.GetFaceNeighbours().ToList().Count < 4);

    //Size | Scale Parameters
    public int VoxelCount => Voxels.Count; //In voxel units
    public float Area => (_grid.VoxelSize * _grid.VoxelSize) * VoxelCount; //In square meters

    //Average dimensions in the X and Z directions. 
    //Does not ignore jagged edges / broken lengths of the space
    //Use is still unclear, might help later

    public float AverageXWidth => (int) Voxels.GroupBy(v => v.Index.z).Select(r => r.ToList().Count).Average() * _grid.VoxelSize; //In meters

    public float AverageZWidth => (int)Voxels.GroupBy(v => v.Index.x).Select(r => r.ToList().Count).Average() * _grid.VoxelSize; //In meters

    //Defines if a space should be regarded as spare given its average widths and area 
    public bool IsSpare => AverageXWidth < 2.20f || AverageZWidth < 2.20f || Area < 4.0f? true : false;

    //Connectivity Parameters

    /// <summary>
    /// Get from the boundary voxels, the ones that represent connections to other spaces
    /// </summary>
    public IEnumerable<Voxel> ConnectionVoxels => 
        BoundaryVoxels.Where(v => 
        v.GetFaceNeighbours()
        .Any(n => n.ParentSpace != this && n.InSpace));

    /// <summary>
    /// The number of voxels connecting this space to others
    /// </summary>
    public int NumberOfConnections => ConnectionVoxels.Count();

    /// <summary>
    /// The ratio (0.00 -> 1.00) between the number of voxels on the boundary of the space and the amount of voxels that are connected to other spaces
    /// </summary>
    public float ConnectionRatio => (float)Math.Round((float)NumberOfConnections / BoundaryVoxels.Count(), 2);

    /// <summary>
    /// The spaces that are connected to this one
    /// </summary>
    public IEnumerable<PPSpace> NeighbourSpaces
    {
        get
        {
            HashSet<PPSpace> tempSpaces = new HashSet<PPSpace>();
            foreach (var voxel in ConnectionVoxels)
            {
                var neighbours = voxel.GetFaceNeighbours();
                foreach (var neighbour in neighbours)
                {
                    var nSpace = neighbour.ParentSpace;
                    if (nSpace != this)
                    {
                        tempSpaces.Add(nSpace);
                    }
                }
            }
            return tempSpaces.Distinct().Where(s => s != null);
        }
    }

    /// <summary>
    /// A Dictionary representing the connection lenght in voxel units between this space and its neighbours
    /// </summary>
    public Dictionary<PPSpace,int> ConnectionLenghts
    {
        get
        {
            Dictionary<PPSpace, int> tempDictionary = new Dictionary<PPSpace, int>();
            foreach (var space in NeighbourSpaces)
            {
                var t = ConnectionVoxels.Count(v => v.GetFaceNeighbours().Any(n => n.ParentSpace == space));
                tempDictionary.Add(space, t);
            }
            return tempDictionary;
        }
    }

    //Scoring fields and properties
    public bool Reconfigure => Reconfigure_Area || Reconfigure_Connectivity ? true : false;
    public int TimesUsed = 0;

    public bool Reconfigure_Area = false;
    public float AreaScore = 0.50f;
    private float _areaRating = 0.00f;
    private int _areaIncrease = 0;
    private int _areaDecrease = 0;

    public bool Reconfigure_Connectivity = false;
    public float ConnectivityScore = 0.50f;
    private float _connectivityRating = 0.00f;
    private int _connectivityIncrease = 0;
    private int _connectivityDecrease = 0;

    private string _operationMessage;
    
    //
    //CONSTRUCTORS
    //
    public PPSpace(VoxelGrid grid)
    {
        _grid = grid;
    }

    /// <summary>
    /// Generic constructor
    /// </summary>
    public PPSpace() { }
    //
    //METHODS AND FUNCTIONS
    //
    public PPSpace NewSpace(VoxelGrid grid, string name)
    {
        //Method to create new spaces, read from a JSON file
        //Still not sure if this is unecessarily creating extra spaces [ IT PROBABLY IS :( ]
        //Revision to remove this method later is necessary. The constructor will be enough
        PPSpace s = new PPSpace();
        s.OCIndexes = OCIndexes;
        s._grid = grid;

        var indexes = s.OCIndexes.Split(';');
        int len = indexes.Length;
        s.Indices = new HashSet<Vector3Int>();
        s.Voxels = new HashSet<Voxel>();
        for (int i = 0; i < len; i++)
        {
            var index = indexes[i];
            var coords = index.Split('_');
            Vector3Int vector = new Vector3Int(int.Parse(coords[0]), int.Parse(coords[1]), int.Parse(coords[2]));
            var voxel = grid.Voxels[vector.x, vector.y, vector.z];
            voxel.ParentSpace = s;
            voxel.InSpace = true;
            s.Indices.Add(vector);
            s.Voxels.Add(voxel);
        }
        s.Name = name;
        s.CreateArrow();
        return s;
    }

    /// <summary>
    /// Destrous the space by clearing the voxels in the grid
    /// </summary>
    /// <returns></returns>
    public List<Voxel> DestroySpace()
    {
        //Destroys a space by removing and cleaning its voxels beforehand
        List<Voxel> orphans = new List<Voxel>();
        foreach (var voxel in Voxels)
        {
            voxel.InSpace = false;
            voxel.ParentSpace = null;
            orphans.Add(voxel);
        }
        Voxels = new HashSet<Voxel>();
        _infoArrow.GetComponent<InfoArrow>().SelfDestroy();
        return orphans;
    }

    /// <summary>
    /// Occupies the space by a tenant according to a request
    /// </summary>
    /// <param name="request">The request that summoned the space</param>
    public void OccupySpace(PPSpaceRequest request)
    {
        Occupied = true;
        _usedRequest = request;
        _durationLeft = _usedRequest.Duration;
        _occupyingTenant = request.Tenant;
        _occupyingTenant.SetSpaceToIcon(this, _grid);
    }

    /// <summary>
    /// Iterates the use of the space by a Tenant through time
    /// </summary>
    /// <returns>Helper string</returns>
    public string UseSpace()
    {
        if (_durationLeft == 0)
        {
            TimesUsed++;
            ReleaseSpace();
            return _operationMessage;
        }
        else
        {
            _durationLeft--;
            return "IGNORE";
        }
    }

    /// <summary>
    /// Releases the space from the tenant and request
    /// </summary>
    void ReleaseSpace()
    {
        EvaluateSpace();
        _occupyingTenant.ReleaseIcon();
        Occupied = false;
        _usedRequest = null;
        _durationLeft = 0;
        _occupyingTenant = null;
        //Debug.Log($"{Name} has been released");
    }

    /// <summary>
    /// Calls the evaluation of the space's Area and Connectivity
    /// </summary>
    void EvaluateSpace()
    {
        EvaluateSpaceArea();
        EvaluateSpaceConnectivity();
    }

    /// <summary>
    /// Evaluates the space area based on the <see cref="Tenant"/>'s preferences
    /// </summary>
    void EvaluateSpaceArea()
    {
        //Reading and Evaluation is ok, positive feedback diferentiation / scale still not implemented
        var requestFunction = _usedRequest.Function;
        var tenantAreaPref = _occupyingTenant.AreaPreferences[requestFunction];
        var tenantAreaMin = tenantAreaPref[0]; //This is voxel units per person
        var tenantAreaMax = tenantAreaPref[1]; //This is voxel units per person

        if (VoxelCount < tenantAreaMin * _usedRequest.Population)
        {
            _areaIncrease++;
            //Debug.Log($"{_occupyingTenant.Name} Feedback: {Name} too small");
            _operationMessage = $"Tenant {_occupyingTenant.Name} Feedback: {Name} too small";
        }
        else if (VoxelCount > tenantAreaMax * _usedRequest.Population)
        {
            _areaDecrease++;
            //Debug.Log($"{_occupyingTenant.Name} Feedback: {Name} too big");
            _operationMessage = $"Tenant {_occupyingTenant.Name} Feedback: {Name} too big";
        }
        else
        {
            _areaRating += 1.00f;
            //Debug.Log($"{_occupyingTenant.Name} Feedback: {Name} good enough");
            _operationMessage = $"Tenant {_occupyingTenant.Name} Feedback: {Name} good enough";
        }

        //Update area score
        AreaScore = _areaRating / TimesUsed;
    }

    /// <summary>
    /// Evaluates the space connectivity based on the <see cref="Tenant"/>'s preferences
    /// </summary>
    void EvaluateSpaceConnectivity()
    {
        var requestFunction = _usedRequest.Function;
        var tenantConnectPref = _occupyingTenant.ConnectivityPreferences[requestFunction];
        var tenantConnectMin = tenantConnectPref[0]; //This is a float (percentage)
        var tenantConnectMax = tenantConnectPref[1]; //This is a float (percentage)

        if (ConnectionRatio < tenantConnectMin)
        {
            _connectivityIncrease++;
            //Debug.Log($"{_occupyingTenant.Name} Feedback: {Name} too isolated, wanted {tenantConnectMin}, was {ConnectionRatio}");
            _operationMessage = $"Tenant {_occupyingTenant.Name} Feedback: {Name} too isolated, wanted {tenantConnectMin}, was {ConnectionRatio}";
        }
        else if (ConnectionRatio > tenantConnectMax)
        {
            _connectivityDecrease++;
            //Debug.Log($"{_occupyingTenant.Name} Feedback: {Name} not private enough");
            _operationMessage = $"Tenant {_occupyingTenant.Name} Feedback: {Name} not private enough";
        }
        else
        {
            _connectivityRating += 1.00f;
            //Debug.Log($"{_occupyingTenant.Name} Feedback: {Name} good enough");
            _operationMessage = $"Tenant {_occupyingTenant.Name} Feedback: {Name} good enough";
        }

        //Update connectivity score
        ConnectivityScore = _connectivityRating / TimesUsed;
    }

    /// <summary>
    /// Creathes the InfoArrow of the space
    /// </summary>
    public void CreateArrow()
    {
        //Instantiates the InfoArrow GameObject on the average center of the space
        //and sets this space to be referenced by the arrow
        _infoArrow = GameObject.Instantiate(Resources.Load<GameObject>("GameObjects/InfoArrow"));
        _infoArrow.name = "Space_" + Name;
        //_infoArrow.transform.position = _grid.GridGO.transform.position + _grid.Origin + _center + new Vector3(0,1.75f,0);
        _infoArrow.transform.SetParent(_grid.GridGO.transform.parent);
        _infoArrow.transform.localPosition = _center + new Vector3(0, 1.75f, 0);
        _infoArrow.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
        _infoArrow.GetComponent<InfoArrow>().SetSpace(this);
    }

    /// <summary>
    /// Sets the visibility of the InfoArrow GameObject
    /// </summary>
    /// <param name="visible"></param>
    public void InfoArrowVisibility(bool visible)
    {
        //Sets the visibility / state of the space's InfoArrow
        _infoArrow.SetActive(visible);
    }

    /// <summary>
    /// Gets the information from the space
    /// </summary>
    /// <returns>The formated infomation</returns>
    public string GetSpaceInfo()
    {
        string output = "";
        string tab = "  ";
        string breakLine = "\n";
        
        string nameHeader = $"[{Name}]";

        string spare = "[Not spare]";
        if (IsSpare)
        {
            spare = "[Spare space]";
        }

        string sizeHeader = $"[Size Parameters]";
        string area = $"Area: {Area.ToString("F", new CultureInfo("en-US"))} m²";
        string averageX = $"Average X Width: {AverageXWidth.ToString("F", new CultureInfo("en-US"))} m";
        string averageZ = $"Average Z Width: {AverageZWidth.ToString("F", new CultureInfo("en-US"))} m";

        string connectivityHeader = $"[Connectivity Parameters]";
        string connections = $"Connections: {NumberOfConnections} voxels";
        string boundary = $"Boundary Length: {BoundaryVoxels.Count()} voxels";
        string connectivityRatio = $"Connectivity Ratio: {ConnectionRatio}";

        string neighboursHeader = "[Neighbours]";
        string neighbours = "";
        foreach (var neighbour in NeighbourSpaces)
        {
            string name = neighbour.Name;
            string length = ConnectionLenghts[neighbour].ToString();

            neighbours += tab + tab + name + ": " + length + "voxels" + breakLine;

        }

        string usageHeader = "[Usage Data]";
        string timesUsed = $"Times used: {TimesUsed.ToString()}";
        string areaCurrentRating = $" Current Area Rating: {_areaRating.ToString()}";
        string areaScore = $"Area Score: {AreaScore.ToString()}";
        string areaReconfigText;

        string connectivityCurrentRating = $" Current Conect. Rating: {_connectivityRating.ToString()}";
        string connectivityScore = $"Connect. Score: {ConnectivityScore.ToString()}";
        string connectivityReconfigText;


        if (Reconfigure_Area)
        {
            if (_areaDecrease > _areaIncrease)
            {
                areaReconfigText = $"Reconfiguration for Area reduction requested";
            }
            else
            {
                areaReconfigText = $"Reconfiguration for Area increment requested";
            }
        }
        else
        {
            areaReconfigText = "No reconfiguration required for Area";
        }
        
        if (Reconfigure_Connectivity)
        {
            if (_connectivityDecrease > _connectivityIncrease)
            {
                connectivityReconfigText = $"Reconfiguration for Connectivity reduction requested";
            }
            else
            {
                connectivityReconfigText = $"Reconfiguration for Connectivity increase requested";
            }
        }
        else
        {
            connectivityReconfigText = "No reconfiguration required for Connectivity";
        }

        output = nameHeader + breakLine +
            spare + breakLine +
            sizeHeader + breakLine +
            tab + area + breakLine +
            tab + averageX + breakLine +
            tab + averageZ + breakLine +
            breakLine +
            connectivityHeader + breakLine +
            tab + connections + breakLine +
            tab + boundary + breakLine +
            tab + connectivityRatio + breakLine +
            tab + neighboursHeader + breakLine +
            neighbours + breakLine + 
            usageHeader + breakLine + 
            tab + timesUsed + breakLine +
            tab + areaCurrentRating + breakLine +
            tab + areaScore + breakLine + 
            tab + areaReconfigText + breakLine + 
            tab + connectivityCurrentRating + breakLine +
            tab + connectivityScore + breakLine +
            tab + connectivityReconfigText
            ;

        return output;
    }

    /// <summary>
    /// Gets the averaged center of the space
    /// </summary>
    /// <returns>The center <see cref="Vector3"/></returns>
    public Vector3 GetCenter()
    {
        return _center;
    }

    /// <summary>
    /// Compare this space to another, given its indexes, to define if they can be regarded as the same
    /// </summary>
    /// <param name="other">The other space</param>
    /// <param name="otherIndexes">The indices of the other space</param>
    /// <returns>The comparison boolean result</returns>
    public bool CompareSpaces(PPSpace other, HashSet<Vector3Int> otherIndexes)
    {
        float percentCap = 0.8f;
        int sizeCap = 8;
        float big;
        float small;
        //First check if the size is beyond cap
        if (VoxelCount >= other.VoxelCount)
        {
            big = VoxelCount * 1.00f;
            small = otherIndexes.Count * 1.00f;
        }
        else
        {
            big = otherIndexes.Count * 1.00f;
            small = VoxelCount * 1.00f;
            
        }
        if (small / big < percentCap && big - small > sizeCap)
        {
            return false;
        }

        //Check the intersection
        int intersection = Indices.Intersect(other.Indices).Count();
        if (intersection /big < percentCap)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    //Equality checking
    public bool Equals(PPSpace other)
    {
        return (other != null && Voxels.Count == other.Voxels.Count && Voxels.All(other.Voxels.Contains));
    }
    public override int GetHashCode()
    {
        //return Voxels.Sum(v => v.GetHashCode());
        return Voxels.GetHashCode();
    }
}

public class PPSpaceCollection
{
    //Class to hold the data read from the JSON file
    public PPSpace[] Spaces;
}