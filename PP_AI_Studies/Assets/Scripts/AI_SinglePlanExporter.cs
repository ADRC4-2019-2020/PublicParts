using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class AI_SinglePlanExporter : MonoBehaviour
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

    void MakeBigPlanAndSave()
    {
        _gridSize = new Vector3Int(150, 1, 150);
        _grid = new VoxelGrid(_gridSize, _voxelSize, Vector3.zero);
        
        int componentCount = _grid.ActiveVoxelsAsList().Count(v => !v.IsOccupied) / 80;
        string prefix = "bigplan";
        PopulateRandomConfigurableAndSave(componentCount, 10, prefix);
    }

    void PopulateRandomConfigurableAndSave(int amt, int variations, string prefix)
    {
        for (int n = 0; n < variations; n++)
        {
            _grid.ClearGrid();
            _existingParts = new List<Part>();
            for (int i = 0; i < amt; i++)
            {
                ConfigurablePart p = new ConfigurablePart(_grid, _existingParts, n);
                _existingParts.Add(p);
            }
            ImageReadWrite.WriteGrid2Image(_grid, n, prefix);
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
            MakeBigPlanAndSave();
        }

        //Output Message
        GUI.Box(new Rect(leftPad, (topPad) + ((fieldHeight + 10) * i++), (fieldTitleWidth + leftPad + textFieldWidth), fieldHeight), _outputMessage, "outputMessage");
    }
}
