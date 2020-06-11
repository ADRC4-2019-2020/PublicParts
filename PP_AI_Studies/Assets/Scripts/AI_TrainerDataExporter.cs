﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class AI_TrainerDataExporter : MonoBehaviour
{
    [SerializeField] GUISkin _skin;
    [SerializeField] Transform _cameraPivot;
    Camera _cam;
    VoxelGrid _grid;
    Vector3Int _gridSize;

    float _voxelSize = 0.375f;
    int _ammountOfComponents;
    List<Part> _existingParts = new List<Part>();
    string _outputMessage;

    void Start()
    {
        _cam = Camera.main;
    }

    void Update()
    {
        DrawState();
    }

    void ReadSaveTrainingData()
    {
        DirectoryInfo folder = new DirectoryInfo(Application.dataPath + "/Resources/Input Data/TrainingData");
        var trainingSet = folder.GetDirectories();
        foreach (var set in trainingSet)
        {
            string[] dimensions = set.Name.Split('_');
            int xSize = int.Parse(dimensions[0]);
            int zSize = int.Parse(dimensions[1]);
            _ammountOfComponents = (xSize * zSize) / 144;
            
            //iterate through the type of each set
            string[] slabType = { "A", "B", "C", "D" };
            foreach (var type in slabType)
            {
                _gridSize = new Vector3Int(xSize, 1, zSize);
                _grid = new VoxelGrid(_gridSize, _voxelSize, Vector3.zero);
                _existingParts = new List<Part>();
                
                //set the states of the voxel grid
                string statesFile = "Input Data/TrainingData/" + set.Name + "/"+ type + "_SlabStates";
                CSVReader.SetGridState(_grid, statesFile);
                
                //populate structure
                string structureFile = "Input Data/TrainingData/" + set.Name + "/" + type + "_Structure";
                ReadStructure(structureFile);
                
                //populate knots
                //string knotsFile = "Input Data/TrainingData/" + set.Name + "/" + type + "_Knots";
                //ReadKnots(knotsFile);

                //populate configurables
                string prefix = set.Name + "_" +type ;
                int componentCount = _grid.ActiveVoxelsAsList().Count(v => !v.IsOccupied) / 120;
                print($"Grid {xSize} x {zSize} with {componentCount}");
                PopulateRandomConfigurableAndSave(componentCount, 10, prefix);
            }
        }
    }

    void PopulateRandomConfigurableAndSave(int amt, int variations, string prefix)
    {
        for (int n = 0; n < variations; n++)
        {
            _grid.ClearGrid();
            _existingParts = new List<Part>();
            for (int i = 0; i < amt; i++)
            {
                ConfigurablePart p = new ConfigurablePart(_grid, false, n);
                _existingParts.Add(p);
            }
            ImageReadWrite.WriteGrid2Image(_grid, n, prefix);
        }
    }

    void ReadStructure(string file)
    {
        var newParts = JSONReader.ReadStructureAsList(_grid, file);
        foreach (var item in newParts)
        {
            _existingParts.Add(item);
        }
    }

    void ReadKnots(string file)
    {
        var newParts = JSONReader.ReadKnotsAsList(_grid, file);
        foreach (var item in newParts)
        {
            _existingParts.Add(item);
        }
    }

    void DrawState()
    {
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                for (int z = 0; z < _gridSize.z; z++)
                {
                    if (_grid.Voxels[x, y, z].IsOccupied)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            var voxel = _grid.Voxels[x, y, z];
                            if (voxel.Part.Type == PartType.Configurable)
                            {
                                Drawing.DrawConfigurable(_grid.Voxels[x, y, z].Center + new Vector3(0, (i + 1) * _voxelSize, 0), _grid.VoxelSize, 1);
                            }
                            else
                            {
                                Drawing.DrawCube(_grid.Voxels[x, y, z].Center + new Vector3(0, (i + 1) * _voxelSize, 0), _grid.VoxelSize, 1);
                            }

                        }

                    }
                    if (_grid.Voxels[x, y, z].IsActive)
                    {
                        Drawing.DrawCube(_grid.Voxels[x, y, z].Center, _grid.VoxelSize, 0);
                    }
                }
            }
        }
    }

    //GUI Controls and Settings
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


        //Logo
        GUI.DrawTexture(new Rect(leftPad, -10, 128, 128), Resources.Load<Texture>("Textures/PP_Logo"));

        //Background Transparency
        GUI.Box(new Rect(leftPad, topPad - 75, (fieldTitleWidth * 2) + (leftPad * 3), (fieldHeight * 25) + 10), Resources.Load<Texture>("Textures/PP_TranspBKG"), "backgroundTile");

        //Setup title
        GUI.Box(new Rect(leftPad, topPad - 40, fieldTitleWidth, fieldHeight + 10), "Control Panel", "partsTitle");

        //Title
        GUI.Box(new Rect(180, 30, 500, 25), "AI Plan Exporter", "title");



        //Output message to be displayed out of test mode
        _outputMessage = "";

        //Populate Button and save several
        if (GUI.Button(new Rect(leftPad, topPad + ((fieldHeight + 10) * i++), (fieldTitleWidth + leftPad + textFieldWidth), fieldHeight), "Populate Parts and Export"))
        {
            ReadSaveTrainingData();
        }

        //Output Message
        GUI.Box(new Rect(leftPad, (topPad) + ((fieldHeight + 10) * i++), (fieldTitleWidth + leftPad + textFieldWidth), fieldHeight), _outputMessage, "outputMessage");
    }
}