using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Diagnostics;
using System;
using System.IO.Abstractions;

public class PP_TrainingEnvironment : PP_Environment
{
    #region Unity methods

    /// <summary>
    /// Configures the Environment, based on 
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
        _saveImageSteps = false;
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
        _cameraPivot.position = new Vector3(MainGrid.Size.x / 2, 0, MainGrid.Size.z / 2) * _voxelSize;

        //Create Configurable Parts
        //PopulateRandomConfigurables(_nComponents);
        //AnalyzeGridCreateNewSpaces();


        //UPDATING
        //Create the configurable
        //PopSeed = 22;
        CreateBlankConfigurables();
    }

    private void Update()
    {
        
        if (_showVoxels)
        {
            DrawState();
        }
        //DrawBoundaries();
        if (_showRawBoundaries)
        {
            DrawBoundaries();
        }

        if (_showSpaces)
        {
            DrawSpaces();
        }

        if (Input.GetMouseButtonDown(0))
        {
            GetSpaceFromArrow();
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            _showVoxels = !_showVoxels;
            SetGameObjectsVisibility(_showVoxels);
        }
        if (_showCompleted)
        {
            DrawCompletedSpace();
        }

        //DrawActiveComponent();

        //Check number of initialized agents and evaluate grid
        if (InitializedAgents == _nComponents)
        {
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

            }
            foreach (ConfigurablePart part in _existingParts.OfType<ConfigurablePart>())
            {
                part.OccupyVoxels();
            }
            AnalyzeGridCreateNewSpaces();
            SetRandomSpaceToReconfigure();
            InitializedAgents = 0;
        }
    }

    #endregion


    #region Architectural functions and methods

    /// <summary>
    /// Initializes blank, not yet applied to the grid, <see cref="ConfigurablePart"/>
    /// </summary>
    private void CreateBlankConfigurables()
    {
        for (int i = 0; i < _nComponents; i++)
        {
            ConfigurablePart p = new ConfigurablePart(MainGrid, !_showVoxels, $"CP_{i}");
            MainGrid.ExistingParts.Add(p);
            _existingParts.Add(p);
        }
    }

    #endregion


    #region GUI Controls and Settings
    
    private void OnGUI()
    {
        GUI.skin = _skin;
        GUI.depth = 2;
        int leftPad = 20;
        int topPad = 200;
        int fieldHeight = 25;
        int fieldTitleWidth = 110;
        int textFieldWidth = 125;
        int i = 1;

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
                Color acid = new Color(0.85f, 1.0f, 0.0f, 0.5f);
                Color grey = new Color(0f, 0f, 0f, 0.5f);

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
            Vector2 tagSize = new Vector2(64, 20);
            foreach (var space in spaces)
            {
                if (!space.IsSpare)
                {
                    string spaceName = space.Name;
                    Vector3 tagWorldPos = transform.position + space.GetCenter() + (Vector3.up * tagHeight);

                    var t = _cam.WorldToScreenPoint(tagWorldPos);
                    Vector2 tagPos = new Vector2(t.x - (tagSize.x / 2), Screen.height - t.y);

                    GUI.Box(new Rect(tagPos, tagSize), spaceName, "spaceTag2");
                }
            }
        }
    }

    #endregion
}
