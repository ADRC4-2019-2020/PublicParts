using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.MLAgents;
using UnityEngine;


public class PP_TrainingEnvironment : PP_Environment
{
    #region Fields and Properties

    public GUISkin _skin;
    private int[] _availableSeeds;

    #endregion

    #region Unity methods

    /// <summary>
    /// Configures the Environment before start
    /// </summary>
    private void Awake()
    {
        _availableSeeds = new int[4] { 666, 555, 444, 66 };
        _nComponents = 5;
        _voxelSize = 0.375f;
        _existingParts = new List<Part>();
        _spaces = new List<PPSpace>();
        _boundaries = new List<Voxel>();
        _tenants = new List<Tenant>();
        _spaceRequests = new List<PPSpaceRequest>();
        _reconfigurationRequests = new List<ReconfigurationRequest>();

        _hourStep = 0.05f;
        InitializedAgents = 0;
        _showDebug = true;
        _compiledMessage = new string[2];
        _showRawBoundaries = false;
        _showSpaces = true;
        _showSpaceData = false;
        _showVoxels = true;
        _activityLog = "";
        //_saveImageSteps = false;
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
        _tenants = JSONReader.ReadTenantsWithPreferences("Input Data/U_TenantPreferences", MainGrid);
        _spaceRequests = JSONReader.ReadSpaceRequests("Input Data/U_SpaceRequests", _tenants);

        CreateBlankConfigurables();
    }

    private void Update()
    {
        if (_showVoxels) DrawState();

        if (_showRawBoundaries) DrawBoundaries();

        if (_showSpaces) DrawSpaces();

        if (Input.GetMouseButtonDown(0)) GetSpaceFromArrow();

        if (Input.GetKeyDown(KeyCode.V))
        {
            _showVoxels = !_showVoxels;
            SetGameObjectsVisibility(_showVoxels);
        }

        if (_showCompleted) DrawCompletedSpace();

        if (Input.GetKeyDown(KeyCode.Space))
        {
            var request = _reconfigurationRequests[0];

            AnalyzeGridUpdateSpaces();
            int reconfigResult = CheckResultFromRequest(request);

            if (reconfigResult == 0)
            {
                //Reconfiguration was just valid, slight penalty to all agents
                request.ApplyReward(-0.1f);
                ResetGrid(request, false);
            }
            else if (reconfigResult == 1)
            {
                //Reconfiguration was successful, add reward to all agents
                request.ApplyReward(1.0f);
                ResetGrid(request, true);
            }
            else if (reconfigResult == 2)
            {
                //Reconfiguration destroyed the space, heavy penalty to all agents
                request.ApplyReward(-1.0f);
                ResetGrid(request, false);
            }

            _reconfigurationRequests = new List<ReconfigurationRequest>();
        }
    }

    /// <summary>
    /// Used to keep the actions, initializations and rewards synchronized with the agents
    /// </summary>
    private void FixedUpdate()
    {
        //Check number of initialized agents and evaluate grid in the start of an episode
        if (InitializedAgents == _nComponents)
        {
            InitializeGrid();
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
                    request.ApplyReward(-0.5f);
                    ResetGrid(request, false);
                }
                else if (reconfigResult == 1)
                {
                    //Reconfiguration was successful, add reward to all agents
                    request.ApplyReward(2.0f);
                    ResetGrid(request, true);
                }
                else if (reconfigResult == 2)
                {
                    //Reconfiguration destroyed the space, heavy penalty to all agents
                    request.ApplyReward(-2.0f);
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

    #endregion


    #region Architectural functions and methods

    /// <summary>
    /// Initializes the grid by assigning new positions for the components given the <see cref="_availableSeeds"/>,
    /// commits their positions, analyze the grid to create the spaces and sets a random space to be reconfigured
    /// </summary>
    private void InitializeGrid()
    {
        InitializedAgents = 0;
        PopSeed = _availableSeeds[UnityEngine.Random.Range(0, 4)];
        foreach (ConfigurablePart part in _existingParts.OfType<ConfigurablePart>())
        {
            int attempt = 0;
            bool success = false;
            while (!success)
            {
                part.FindNewPosition(PopSeed + attempt, out success);
                attempt++;
            }
            //part.OccupyVoxels();
        }

        //foreach (ConfigurablePart part in _existingParts.OfType<ConfigurablePart>())
        //{
        //    part.OccupyVoxels();
        //}
        AnalyzeGridCreateNewSpaces();
        //int areaRequest = Random.Range(-1, 2);
        int areaRequest = 0;
        int connectivityRequest = Random.Range(-1, 2);
        //int connectivityRequest = 0;
        SetRandomSpaceToReconfigure(areaRequest, connectivityRequest);
        //InitializedAgents = 0;
    }

    #endregion

    #region GUI Controls and Settings
    
    private void OnGUI()
    {
        GUI.skin = _skin;
        GUI.depth = 2;
        //Draw Spaces tags
        //DrawSpaceTags();
    }

    #endregion

    #region Drawing and visualization

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
