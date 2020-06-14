using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Diagnostics;
using QuickGraph;
using QuickGraph.Algorithms;

/// <summary>
/// Updating class to run save pix2pix directly from unity using Barracuda
/// This version no longer implements the non-AI method of
/// space recognition and no longer utilizes the PP_p2p_Generate.py script.
/// </summary>
public class AI_SpaceRecognition : MonoBehaviour
{
    //
    //Fields and Parameters
    //

    //Object inputs
    [SerializeField] GUISkin _skin;
    [SerializeField] Transform _cameraPivot;
    Camera _cam;
    VoxelGrid _grid;
    Vector3Int _gridSize;

    //Grid setup
    //Currently available slabs: 44_44_A, 50_32_C, 38_26_C, 44_32_C
    string _gridName = "44_44";
    string _gridType = "A";
    GameObject _gridGO;
    //Seed to run the population method
    int _popSeed = 5;

    float _voxelSize = 0.375f;

    //Pix2pix inference object
    PP_pix2pix _pix2pix;

    //Grid data and objects collections
    List<Part> _existingParts = new List<Part>();
    List<PPSpace> _spaces = new List<PPSpace>();
    List<Voxel> _boundaries = new List<Voxel>();
    List<Tenant> _tenants = new List<Tenant>();
    List<PPSpaceRequest> _spaceRequests = new List<PPSpaceRequest>();

    int _frame = 0;

    int _day = 0;
    int _hour = 0;
    float _hourStep = 0.05f; //in seconds, represents a virtual hour
    bool _timePause;
    string _dateTimeNow;
    string[] _weekdaysNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
    int _currentWeekDay = 0;

    bool _progressionRunning = false;

    //Debugging
    bool _showDebug = true;
    string _debugMessage;
    string[] _compiledMessage = new string[2];

    bool _showTime;
    bool _showRawBoundaries = false;
    bool _showSpaces = true;
    bool _showSpaceData = false;

    bool _showVoxels = true;

    string _outputMessage;
    string _spaceData;
    string _activityLog = "";

    PPSpace _selectedSpace;

    void Start()
    {
        _cam = Camera.main;

        CreateGridFromFile();

        //Load tenants and requests data
        _tenants = JSONReader.ReadTenantsWithPreferences("Input Data/U_TenantPreferences", _grid);
        _spaceRequests = JSONReader.ReadSpaceRequests("Input Data/U_SpaceRequests", _tenants);
        _cameraPivot.position = new Vector3(_gridSize.x / 2, 0, _gridSize.z / 2) * _voxelSize;

        //Create the pix2pix object
        _pix2pix = new PP_pix2pix();
    }


    void Update()
    {
        if (_showVoxels)
        {
            DrawState();
        }
        
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

        //StartCoroutine(SaveScreenshot());
    }

    //
    //Architectural functions and methods
    //

    /// <summary>
    /// Executes the Pix2pix model, infering from the external Python script, and generates the spaces of the building
    /// </summary>
    void ExecuteAI()
    {
        Stopwatch aiStopwatch = new Stopwatch();
        aiStopwatch.Start();
        PopulateAndAnalyseGrid();
        GenerateSpaces();
        aiStopwatch.Stop();
        var t = aiStopwatch.ElapsedMilliseconds;
        _activityLog = $"AI Message: Generated {_spaces.Count} Spaces in {t} ms";
    }

    /// <summary>
    /// Executes the internal Barracuda Pix2pix model, inferring from GPU and generating the respective spaces
    /// </summary>
    void NewAIAnalysis()
    {
        Stopwatch aiStopwatch = new Stopwatch();
        aiStopwatch.Start();
        PopulateAndAnalyseGrid();
        GenerateSpaces();
        aiStopwatch.Stop();
        var t = aiStopwatch.ElapsedMilliseconds;
        _activityLog = $"AI Message: Generated {_spaces.Count} Spaces in {t} ms";
    }

    /// <summary>
    /// Generate spaces on the voxels that are not inside the parts boudaries, or space or part
    /// The method is inspired by a BFS algorithm, continuously checking the neighbours of the
    /// processed voxels until the minimum area is reached. It is designed to be called in a loop 
    /// that feeds the numbering / naming of the spaces
    /// </summary>
    /// <param name="number">Current number of the space</param>
    void GenerateSingleSpace(int number)
    {
        int maximumArea = 1000; //in voxel ammount
        var availableVoxels = _grid.ActiveVoxelsAsList().Where(v => !_boundaries.Contains(v) && !v.IsOccupied && !v.InSpace).ToList();
        if (availableVoxels.Count == 0) return;
        Voxel originVoxel = availableVoxels[0];

        //Initiate a new space
        PPSpace space = new PPSpace(_grid);
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
                    var gridVoxel = _grid.Voxels[nIndex.x, nIndex.y, nIndex.z];
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
        space.CreateArrow();
        _spaces.Add(space);
    }

    /// <summary>
    /// Generate spaces by looping <see cref="GenerateSingleSpace(int)"/> until all voxels are 
    /// assigned a space
    /// </summary>
    void GenerateSpaces()
    {
        //Destroy existing spaces
        foreach (var space in _spaces) space.DestroySpace();

        //Clear spaces list
        _spaces = new List<PPSpace>();
        
        int i = 0;
        //Generate spaces on vacant voxels inside boundaries
        while (_grid.ActiveVoxelsAsList().Any(v => !_boundaries.Contains(v) && !v.IsOccupied && !v.InSpace))
        {
            GenerateSingleSpace(i++);
        }


        //Allocate boundary voxel to the smallest neighbouring space
        while (_boundaries.Any(b => !b.InSpace))
        {
            Voxels2SmallestNeighbour(_boundaries.Where(b => !b.InSpace));
        }

        _activityLog = $"AI Message: Generated {_spaces.Count} Spaces";
    }

    /// <summary>
    /// Adds orphan voxels to the smallest neighbouring space
    /// </summary>
    /// <param name="voxels2Allocate">The voxels to be alocated</param>
    void Voxels2SmallestNeighbour(IEnumerable<Voxel> voxels2Allocate)
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
    /// Creates a grid from a file, reading its size, voxel states and structural elements from a given file
    /// </summary>
    void CreateGridFromFile()
    {
        //Read one state from folder
        DirectoryInfo folder = new DirectoryInfo(Application.dataPath + $"/Resources/Input Data/TrainingData/{_gridName}");
        string[] dimensions = folder.Name.Split('_');
        int xSize = int.Parse(dimensions[0]);
        int zSize = int.Parse(dimensions[1]);

        _gridSize = new Vector3Int(xSize, 1, zSize);
        _grid = new VoxelGrid(_gridSize, _voxelSize, Vector3.zero);
        _existingParts = new List<Part>();

        //set the states of the voxel grid
        string statesFile = "Input Data/TrainingData/" + _gridName + "/" + _gridType + "_SlabStates";
        CSVReader.SetGridState(_grid, statesFile);

        //move camera pivot to grid center
        _cameraPivot.position = new Vector3(_gridSize.x / 2, 0, _gridSize.z / 2) * _voxelSize;

        //populate structure
        string structureFile = "Input Data/TrainingData/" + _gridName + "/" + _gridType + "_Structure";
        ReadStructure(structureFile);

        InstantiateGridGO();
    }

    /// <summary>
    /// Instanciates the GameObject that represents the grid
    /// </summary>
    void InstantiateGridGO()
    {
        //instantiate grid GO
        GameObject reference = Resources.Load<GameObject>($"GameObjects/{_gridName}_{_gridType}");
        _gridGO = Instantiate(reference);
        _gridGO.transform.position = Vector3.zero;
        _gridGO.transform.localScale = new Vector3(_voxelSize, _voxelSize, _voxelSize);
        _gridGO.SetActive(!_showVoxels);
    }

    /// <summary>
    /// Condensed method to populate the grid with configurable parts and analyse it with
    /// the Pix2Pix model
    /// </summary>
    void PopulateAndAnalyseGrid()
    {
        //Clean the boundaries
        _boundaries = new List<Voxel>();

        //populate configurables
        int componentCount = _grid.ActiveVoxelsAsList().Count(v => !v.IsOccupied) / 120;
        
        //Populate grid and get resulting texture, already upscaled
        var gridImage = PopulateRandomConfigurableGetImage(componentCount);

        //Analyse grid with Pix2pix
        var analysisResult = _pix2pix.GeneratePrediction(gridImage);

        //Post-process the analysis result texture
        var resultTexture = ProcessAnalysisResult(analysisResult);

        //Assign result pixels to voxel data, populating the boundaries list
        PassBoundaryToList(resultTexture);
        _activityLog = $"AI Message: Generated {componentCount} components";
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

        //Create new texture with the same size as the original grid
        Texture2D resultGridTexture = new Texture2D(_gridSize.x, _gridSize.z);

        //Write result to texture
        for (int i = 0; i < resultGridTexture.width; i++)
        {
            for (int j = 0; j < resultGridTexture.height; j++)
            {
                int x = i;
                int y = analysisResult.height - resultGridTexture.height + j + 1;
                resultGridTexture.SetPixel(i, j, analysisResult.GetPixel(x, y));
            }
        }
        resultGridTexture.Apply();

        //Return post-processed texture
        return PP_ImageProcessing.PostProcessImageFromTexture(resultGridTexture);
    }

    /// <summary>
    /// Translates the pixel data from the texture to the boundary list
    /// </summary>
    /// <param name="texture">The input texture</param>
    void PassBoundaryToList(Texture2D texture)
    {
        Vector2Int gridSize = new Vector2Int(_grid.Size.x, _grid.Size.z);
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
                    _boundaries.Add(_grid.Voxels[x, 0, y]);
                }
            }
        }
    }

    /// <summary>
    /// Clears the grid and populate a given amount of new configurable parts on the grid
    /// </summary>
    /// <param name="amt">The amount of parts to populate</param>
    /// <returns>The Texture that represents the grid state</returns>
    Texture2D PopulateRandomConfigurableGetImage(int amt)
    {
        _grid.ClearGrid();
        var configurables = _existingParts.OfType<ConfigurablePart>();
        foreach (var c in configurables) c.DestroyGO();
        _existingParts = new List<Part>();
        for (int i = 0; i < amt; i++)
        {
            ConfigurablePart p = new ConfigurablePart(_grid, !_showVoxels, _popSeed);
            _existingParts.Add(p);
        }
        //Write image to temp_sr folder
        return ImageReadWrite.TextureFromGrid(_grid);
    }

    /// <summary>
    /// Reads a structre file and creates the parts, feeding them into the _existingParts list
    /// </summary>
    /// <param name="file">The file to be read</param>
    void ReadStructure(string file)
    {
        var newParts = JSONReader.ReadStructureAsList(_grid, file);
        foreach (var item in newParts)
        {
            _existingParts.Add(item);
        }
    }

    /// <summary>
    /// Gets the space that the Arrow object represents
    /// </summary>
    /// <returns>The PPSpace object</returns>
    PPSpace GetSpaceFromArrow()
    {
        //This method allows clicking on the InfoArrow
        //and returns its respective space
        PPSpace clicked = null;
        Ray ClickRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ClickRay, out hit))
        {
            if (hit.collider.gameObject.transform != null && hit.collider.gameObject.tag == "InfoArrow")
            {
                clicked = hit.collider.gameObject.GetComponent<InfoArrow>().GetSpace();
                //print($"Clicked on {clicked.Name}'s arrow");
                _showSpaceData = true;
                _spaceData = clicked.GetSpaceInfo();
                _cameraPivot.position = hit.collider.gameObject.transform.position;
                _selectedSpace = clicked;
            }
        }
        else
        {
            _spaceData = null;
            _selectedSpace = null;
            _showSpaceData = false;
            _cameraPivot.position = new Vector3(_gridSize.x / 2, 0, _gridSize.z / 2) * _voxelSize;
        }
        return clicked;
    }

    //
    // Space utilization functions and methods
    //

    /// <summary>
    /// IEnumerator to run the daily progression of the occupation simulation
    /// </summary>
    /// <returns></returns>
    IEnumerator DailyProgression()
    {
        while (_day < 365)
        {
            if (!_timePause)
            {
                if (_hour % 12 == 0)
                {
                    CheckSpaces();
                    CheckForReconfiguration();
                }
                _dateTimeNow = $"Day {_day}, {_weekdaysNames[_currentWeekDay]}, {_hour}:00";
                float hourProbability = Random.value;
                foreach (var request in _spaceRequests)
                {
                    if (request.StartTime == _hour)
                    {
                        var rProbability = request.RequestProbability[_currentWeekDay];
                        if (rProbability >= hourProbability)
                        {
                            RequestSpace(request);
                        }
                    }
                }
                var occupiedSpaces = _spaces.Where(s => s.Occupied);
                foreach (var space in occupiedSpaces)
                {
                    string useReturn = space.UseSpace();
                    if (useReturn != "IGNORE")
                    {
                        _activityLog = useReturn;
                    }
                }

                NextHour();
                //UpdateSpaceData();
                yield return new WaitForSeconds(_hourStep);
            }
            else
            {
                yield return null;
            }
        }
    }

    /// <summary>
    /// Check if spaces need to be reconfigured
    /// </summary>
    void CheckSpaces()
    {
        foreach (var space in _spaces)
        {
            if (space.TimesUsed > 10)
            {
                if (space.AreaScore < 0.20f)
                {
                    space.Reconfigure_Area = true;
                }
                else
                {
                    space.Reconfigure_Area = false;
                }

                if (space.ConnectivityScore < 0.20f)
                {
                    space.Reconfigure_Connectivity = true;
                }
                else
                {
                    space.Reconfigure_Connectivity = false;
                }
            }
        }
    }

    /// <summary>
    /// Check if there are enough reconfiguration requests to reconfigure the whole plan
    /// NOTE: TEMPORARY METHOD
    /// </summary>
    void CheckForReconfiguration()
    {
        if (_spaces.Count(s => s.Reconfigure) >= 2)
        {
            //ExecuteAI();
        }
    }

    /// <summary>
    /// Attempts to assign a space to a request made by a Tenant
    /// </summary>
    /// <param name="request">The Request object</param>
    void RequestSpace(PPSpaceRequest request)
    {
        var requestArea = request.Population * request.Tenant.AreaPerIndInferred; //Request area assuming the area the tenant prefers per individual
        var availableSpaces = _spaces.Where(s => !s.Occupied && !s.IsSpare);

        PPSpace bestSuited = availableSpaces.MaxBy(s => s.VoxelCount);
        foreach (var space in availableSpaces)
        {
            var spaceArea = space.Area;

            if (spaceArea >= requestArea && spaceArea < bestSuited.VoxelCount)
            {
                bestSuited = space;
            }
        }
        bestSuited.OccupySpace(request);
        _activityLog = $"Assinged {bestSuited.Name} to {request.Tenant.Name} at {_dateTimeNow}";
    }

    /// <summary>
    /// Move simulation to next hour, keeping track of time, day number and weekday
    /// </summary>
    void NextHour()
    {
        _hour++;
        if (_hour % 24 == 0)
        {
            _hour = 0;
            _day++;
            _currentWeekDay++;
        }
        if (_currentWeekDay > 6) _currentWeekDay = 0;
    }

    //
    //Animation and saving
    //
    IEnumerator SaveScreenshot()
    {
        //A SAVER OBJECT AND CLASS SHOULD BE IMPLEMENTED, APART FROM THIS CLASS
        string file = $"SavedFrames/SpaceAnalysis/Frame_{_frame}.png";
        ScreenCapture.CaptureScreenshot(file, 2);
        _frame++;
        yield return new WaitForEndOfFrame();
    }

    //
    //Drawing and Visualizing
    //

    /// <summary>
    /// Change the visibility of the scene's GameObjects, iterating between 
    /// voxel and GameObject visualization
    /// </summary>
    /// <param name="visible">The boolean trigger</param>
    void SetGameObjectsVisibility(bool visible)
    {
        var configurables = _existingParts.OfType<ConfigurablePart>().ToArray();
        if (configurables.Length > 0)
        {
            foreach (var c in configurables)
            {
                c.SetGOVisibility(!visible);
            }
        }
        _gridGO.SetActive(!visible);
    }

    /// <summary>
    /// Draws the current VoxelGrid state with mesh voxels
    /// </summary>
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
                                PP_Drawing.DrawConfigurable(_grid.Voxels[x, y, z].Center + new Vector3(0, (i + 1) * _voxelSize, 0), _grid.VoxelSize, 1);
                            }
                            else
                            {
                                PP_Drawing.DrawCube(_grid.Voxels[x, y, z].Center + new Vector3(0, (i + 1) * _voxelSize, 0), _grid.VoxelSize, 1);
                            }

                        }

                    }
                    if (_grid.Voxels[x, y, z].IsActive)
                    {
                        PP_Drawing.DrawCube(_grid.Voxels[x, y, z].Center, _grid.VoxelSize, 0);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Draws the boundary voxels with meshes
    /// </summary>
    void DrawBoundaries()
    {
        foreach (var voxel in _boundaries)
        {
            PP_Drawing.DrawCubeTransparent(voxel.Center + new Vector3(0f, _voxelSize, 0f), _voxelSize);
        }
    }

    /// <summary>
    /// Represents the spaces with voxel meshes
    /// </summary>
    void DrawSpaces()
    {
        foreach (var space in _spaces)
        {
            Color color = new Color();
            if (space.Reconfigure)
            {
                if (space != _selectedSpace)

                {
                    color = new Color(0.7f, 0.1f, 0.1f, 0.70f);
                }
                else
                {
                    color = new Color(0.90f, 0.70f, 0.0f, 0.70f);
                }
            }
            else
            {
                if (space != _selectedSpace)
                {
                    color = new Color(0.9f, 0.9f, 0.9f, 0.70f);
                }
                else
                {
                    color = new Color(0.85f, 1.0f, 0.0f, 0.70f);
                }
            }
            PP_Drawing.DrawSpace(space, _grid, color);
        }

    }

    /// <summary>
    /// Draws the space tags
    /// </summary>
    void DrawSpaceTags()
    {
        if (_showSpaces)
        {
            float tagHeight = 3.0f;
            Vector2 tagSize = new Vector2(60, 15);
            foreach (var space in _spaces)
            {
                string spaceName = space.Name;
                Vector3 tagWorldPos = space.GetCenter() + (Vector3.up * tagHeight);

                var t = _cam.WorldToScreenPoint(tagWorldPos);
                Vector2 tagPos = new Vector2(t.x - (tagSize.x / 2), Screen.height - t.y);

                GUI.Box(new Rect(tagPos, tagSize), spaceName, "spaceTag");
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

        //Draw Spaces tags
        DrawSpaceTags();

        //Logo
        GUI.DrawTexture(new Rect(leftPad, -10, 128, 128), Resources.Load<Texture>("Textures/PP_Logo"));

        //Background Transparency
        GUI.Box(new Rect(leftPad, topPad - 75, (fieldTitleWidth * 2) + (leftPad * 3), (fieldHeight * 25) + 10), Resources.Load<Texture>("Textures/PP_TranspBKG"), "backgroundTile");

        //Setup title
        GUI.Box(new Rect(leftPad, topPad - 40, fieldTitleWidth, fieldHeight + 10), "Control Panel", "partsTitle");

        //Title
        GUI.Box(new Rect(180, 30, 500, 25), "AI Space Analyzer", "title");

        //Date and Time _dateTimeNow
        GUI.Box(new Rect(Screen.width - 530, 30, 500, 25), _dateTimeNow, "dateTime");

        //UI during Daily progression
        Rect pauseRect = new Rect(leftPad, Screen.height - leftPad - 50, 42, 42);
        if (_progressionRunning)
        {
            //Pause Button
            if (GUI.Button(pauseRect, Resources.Load<Texture>("Textures/PauseButton"), "pauseButton"))
            {
                _timePause = !_timePause;
            }
        }
        //Activity log box
        Rect logRect = new Rect(pauseRect.position + new Vector2(leftPad * 3, 0), new Vector2(900, 42));
        GUI.Box(logRect, _activityLog, "activityLog");




        //Output message to be displayed out of test mode


        //Populate Button and save several - AI
        if (GUI.Button(new Rect(leftPad, topPad + ((fieldHeight + 10) * i++), 
            (fieldTitleWidth + leftPad + textFieldWidth), fieldHeight),
            "Populate and Generate Spaces [AI]"))
        {
            NewAIAnalysis();
        }

        if (_spaces.Any())
        {
            //Start Daily progression of tenants
            if (GUI.Button(new Rect(leftPad, topPad + ((fieldHeight + 10) * i++),
                (fieldTitleWidth + leftPad + textFieldWidth), fieldHeight),
                "Start Daily Progression"))
            {
                _progressionRunning = true;
                StartCoroutine(DailyProgression());
            }
        }
        

        //Output Message
        GUI.Box(new Rect(leftPad, (topPad) + ((fieldHeight + 10) * i++), (fieldTitleWidth + leftPad + textFieldWidth), fieldHeight), _outputMessage, "outputMessage");

        //Debug pop-up window
        if (_showDebug)
        {
            GUI.Window(0, new Rect(Screen.width - leftPad - 300, topPad - 75, 300, (fieldHeight * 25) + 10), DebugWindow, "Debug_Summary");
        }
    }

    //Debug Window
    void DebugWindow(int windowID)
    {
        GUIStyle style = _skin.GetStyle("debugWindow");
        int leftPad = 10;
        int topPad = 10;
        int fieldWidth = 300 - (leftPad * 2);
        int fieldHeight = 25;
        //int buttonWidth = 50;
        int windowSize = (fieldHeight * 25) + 10;

        int count = 1;

        _compiledMessage[0] = "Debug output";

        //Show Raw Boundaries
        if (GUI.Button(new Rect(leftPad, windowSize - ((fieldHeight + topPad) * count++), fieldWidth, fieldHeight), "Raw Boundaries"))
        {
            _showRawBoundaries = !_showRawBoundaries;
        }

        //Show Spaces
        if (GUI.Button(new Rect(leftPad, windowSize - ((fieldHeight + topPad) * count++), fieldWidth, fieldHeight), "Spaces"))
        {
            _showSpaces = !_showSpaces;
            //Change the visibility of the spaces' InfoArrows
            foreach (var space in _spaces)
            {
                space.InfoArrowVisibility(_showSpaces);
            }
        }

        //Debug Message
        _debugMessage = "";
        if (_showSpaces && _showSpaceData)
        {
            _debugMessage = _selectedSpace.GetSpaceInfo();
        }
        else
        {
            for (int i = 0; i < _compiledMessage.Length; i++)
            {
                var line = _compiledMessage[i];
                if (line != "escape")
                {
                    _debugMessage += line + '\n';
                }
            }
        }
        GUI.Box(new Rect(leftPad, topPad, fieldWidth, fieldHeight), _debugMessage, "debugMessage");
    }
}


