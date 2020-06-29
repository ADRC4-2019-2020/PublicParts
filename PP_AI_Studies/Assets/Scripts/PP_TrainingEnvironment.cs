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
        MainGrid = new VoxelGrid(_gridSize, _voxelSize, transform.position, true);
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
        DrawSpaceTags();
    }



    #endregion
}
