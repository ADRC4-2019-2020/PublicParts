using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Barracuda;
using UnityEngine.UI;

public class PP_TestingEnvironment : PP_Environment
{
    #region Fields and Properties

    public GUISkin _skin;
    private int[] _availableSeeds;

    private NNModel _activeModel;

    private int _requestCount = 0;
    private int _successfulReconfigs = 0;

    [SerializeField]
    private Text _requestTotalDisplay;

    [SerializeField]
    private Text _successTotalDisplay;

    [SerializeField]
    private Text _successRateDisplay;

    public PP_ScreenCapture SRecorder;

    #endregion

    #region Unity Methods

    /// <summary>
    /// Configures the Environment before start
    /// </summary>
    private void Awake()
    {
        //_availableSeeds = new int[5] { 22, 14, 87, 4, 32 };
        _availableSeeds = new int[5] { 22, 22, 22, 22, 22 };
        _nComponents = 5;
        _voxelSize = 0.375f;
        _existingParts = new List<Part>();
        _agents = new List<ConfigurablePartAgent>();
        _spaces = new List<PPSpace>();
        _boundaries = new List<Voxel>();
        _tenants = new List<Tenant>();
        _spaceRequests = new List<PPSpaceRequest>();
        _reconfigurationRequests = new List<ReconfigurationRequest>();

        InitializedAgents = 0;
        _showSpaces = true;
        _showVoxels = true;
    }

    private void Start()
    {
        _cam = Camera.main;

        _gridSize = new Vector3Int(30, 1, 24);
        MainGrid = new VoxelGrid(_gridSize, _voxelSize, transform.position, true, true);
        _boundaries = MainGrid.Boundaries;
        _gridGO = MainGrid.GridGO;
        _gridGO.transform.SetParent(transform);

        //Load tenants and requests data
        _tenants = JSONReader.ReadTenantsWithPreferences("Input Data/U_TenantPreferences 01", MainGrid);
        _spaceRequests = JSONReader.ReadSpaceRequests("Input Data/U_SpaceRequests", _tenants);

        CreateBlankConfigurables();
    }

    private void Update()
    {
        if (_showVoxels) DrawState();

        if (_showRawBoundaries) DrawBoundaries();

        if (_showSpaces) DrawSpaces();

        if (Input.GetKeyDown(KeyCode.V))
        {
            _showVoxels = !_showVoxels;
            SetGameObjectsVisibility(_showVoxels);
        }

        if (_showCompleted) DrawCompletedSpace();

        if (_requestCount < 200)
        {
            //Check number of initialized agents and evaluate grid in the start of an episode
            if (InitializedAgents == _nComponents)
            {
                InitializeGrid();

                int areaRequest = Random.Range(-1, 2);
                int connectivityRequest = 0;

                SetRandomSpaceToReconfigure(areaRequest, connectivityRequest);
                _requestCount++;
            }

            //Check if the reconfiguration request is finished
            else if (_reconfigurationRequests != null && _reconfigurationRequests[0] != null)
            {
                var request = _reconfigurationRequests[0];
                if (request.AllAgentsFinished())
                {
                    //print("Checking result");
                    AnalyzeGridUpdateSpaces();
                    int reconfigResult = CheckResultFromRequest(request);

                    if (reconfigResult == 0)
                    {
                        //Reconfiguration was just valid, slight penalty to all agents
                        ResetGrid(request, false);
                    }
                    else if (reconfigResult == 1)
                    {
                        //Reconfiguration was successful, add reward to all agents
                        _successfulReconfigs++;
                        ResetGrid(request, true);
                    }
                    else if (reconfigResult == 2)
                    {
                        //Reconfiguration destroyed the space, heavy penalty to all agents
                        ResetGrid(request, false);
                    }

                    _reconfigurationRequests = new List<ReconfigurationRequest>();
                }
                else
                {
                    request.RequestNextAction();
                }
            }
        }
        else
        {
            print("DONE");
            SRecorder.Record = false;
        }
        UpdateCounters();
    }

    #endregion

    #region Architectural functions and methods

    /// <summary>
    /// Initializes the grid by assigning new positions for the components given the <see cref="_availableSeeds"/>,
    /// commits their positions, analyze the grid to create the spaces and sets a random space to be reconfigured
    /// </summary>
    private void InitializeGrid()
    {
        InitializedAgents = 0;
        //PopSeed = _availableSeeds[UnityEngine.Random.Range(0, 4)];
        foreach (ConfigurablePart part in _existingParts.OfType<ConfigurablePart>())
        {
            int attempt = 0;
            bool success = false;
            while (!success)
            {
                part.FindNewPosition(PopSeed + attempt, out success);
                attempt++;
            }
        }

        AnalyzeGridCreateNewSpaces();
    }

    #endregion

    #region Space utilization methods

    /// <summary>
    /// Sets one of the existing spaces to be reconfigured. 
    /// This overriden method replaces the behaviour brain according to the type of request
    /// </summary>
    protected override void SetRandomSpaceToReconfigure(int areaDirection, int connectivityDirection)
    {
        PPSpace space = new PPSpace();
        bool validRequest = false;
        while (!validRequest)
        {
            UnityEngine.Random.InitState(System.DateTime.Now.Millisecond);
            int i = UnityEngine.Random.Range(0, _spaces.Count);
            space = _spaces[i];

            if (space.BoundaryParts.Count > 0 && !space.IsSpare)
            {
                validRequest = true;
            }
        }

        //Create the reconfiguration request
        ReconfigurationRequest rr = new ReconfigurationRequest(space, areaDirection, connectivityDirection);
        _reconfigurationRequests = new List<ReconfigurationRequest>();
        _reconfigurationRequests.Add(rr);
    }

    #endregion

    #region Drawing and visualization

    /// <summary>
    /// Updates the UI counters utilized to represent the statistics of the test
    /// </summary>
    private void UpdateCounters()
    {
        _requestTotalDisplay.text = $"Requests: {_requestCount}";
        _successTotalDisplay.text = $"Success: {_successfulReconfigs}";
        _successRateDisplay.text = $"Success Rate: {((_successfulReconfigs * 1.00f / _requestCount) * 100).ToString("F2")}%";
    }

    /// <summary>
    /// Draws the current VoxelGrid state with mesh voxels
    /// </summary>
    protected override void DrawState()
    {
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                for (int z = 0; z < _gridSize.z; z++)
                {
                    Vector3 index = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * _voxelSize;
                    //Vector3 index = new Vector3(x , y , z) * _voxelSize;
                    if (MainGrid.Voxels[x, y, z].IsOccupied)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            var voxel = MainGrid.Voxels[x, y, z];
                            if (voxel.Part.Type == PartType.Configurable)
                            {
                                //PP_Drawing.DrawConfigurable(transform.position + _grid.Voxels[x, y, z].Center + new Vector3(0, (i + 1) * _voxelSize, 0), _grid.VoxelSize, 1);
                                PP_Drawing.DrawConfigurable(transform.position + index + new Vector3(0, (i + 1) * _voxelSize, 0), MainGrid.VoxelSize, Color.black);
                            }
                            else
                            {
                                //PP_Drawing.DrawCube(transform.position +  _grid.Voxels[x, y, z].Center + new Vector3(0, (i + 1) * _voxelSize, 0), _grid.VoxelSize, 1);
                                PP_Drawing.DrawCube(transform.position + index + new Vector3(0, (i + 1) * _voxelSize, 0), MainGrid.VoxelSize, 1);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Modified the display of the spaces
    /// </summary>
    protected override void DrawSpaces()
    {
        foreach (var space in MainGrid.Spaces)
        {
            if (!space.IsSpare)
            {
                Color color;
                Color acid = new Color(0.85f, 1.0f, 0.0f, 0.85f);
                Color grey = new Color(0f, 0f, 0f, 0.25f);

                if (space.Reconfigure)
                {
                    if (space != _selectedSpace)
                    {
                        color = acid;
                    }
                    else
                    {
                        color = acid;
                    }
                }
                else
                {
                    if (space != _selectedSpace)
                    {
                        color = grey;
                    }
                    else
                    {
                        color = grey;
                    }
                }
                PP_Drawing.DrawSpaceSurface(space, MainGrid, color, transform.position);
            }
        }
    }

    /// <summary>
    /// Draws the space tags
    /// </summary>
    protected override void DrawSpaceTags()
    {
        if (_showSpaces)
        {
            var spaces = _spaces.Where(s => !s.IsSpare).ToArray();
            float tagHeight = 2.0f;
            Vector2 tagSize = new Vector2(38, 38);
            foreach (var space in spaces)
            {
                if (!space.IsSpare)
                {
                    string spaceName = space.Name;
                    spaceName = $"[{spaceName[spaceName.Length - 1]}]";
                    Vector3 tagWorldPos = transform.position + space.GetCenter() + (Vector3.up * tagHeight);

                    var t = _cam.WorldToScreenPoint(tagWorldPos);
                    Vector2 tagPos = new Vector2(t.x - (tagSize.x / 2), Screen.height - t.y);

                    GUI.Box(new Rect(tagPos, tagSize), spaceName, "spaceTag3");
                }
            }
        }
    }

    #endregion
}
