using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Barracuda;
using UnityEngine.UI;

public class PP_pix2pixEvaluate : PP_Environment
{
    #region Fields and Properties

    public GUISkin _skin;

    #endregion

    private void Awake()
    {
        _nComponents = 5;
        _voxelSize = 0.375f;
        _existingParts = new List<Part>();
        _agents = new List<ConfigurablePartAgent>();
        _spaces = new List<PPSpace>();
        _boundaries = new List<Voxel>();

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

        CreateBlankConfigurables();
    }

    private void Update()
    {
        if (_showVoxels) DrawState();

        if (_showRawBoundaries) DrawBoundaries();

        //Check number of initialized agents and evaluate grid in the start of an episode
        if (InitializedAgents == _nComponents)
        {
            InitializeGrid();

            ResetGridLocal();
        }
    }

    private void InitializeGrid()
    {
        InitializedAgents = 0;
        PopSeed = UnityEngine.Random.Range(0, 99999);
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

        AnalyzeGridCreateNewSpaces(true);
    }

    private void ResetGridLocal()
    {
        MainGrid.RestartGrid();
        _spaces = MainGrid.Spaces;
        _boundaries = MainGrid.Boundaries;
        foreach (ConfigurablePartAgent partAgent in _existingParts.OfType<ConfigurablePart>().Select(p => p.CPAgent))
        {
            partAgent.EndEpisode();
        }
    }
}
