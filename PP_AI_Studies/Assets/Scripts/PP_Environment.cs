using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Diagnostics;
using System;
using System.IO.Abstractions;

/// <summary>
/// Class to manage the environment in which a simulation occurs
/// </summary>
public class PP_Environment : MonoBehaviour
{
    #region Fields and Parameters

    #region Basic

    [SerializeField] GUISkin _skin;
    [SerializeField] Transform _cameraPivot;
    Camera _cam;
    VoxelGrid MainGrid;
    private VoxelGrid _paralellGrid;
    Vector3Int _gridSize;

    #endregion

    #region Grid setup

    //Currently available slabs: 44_44_A
    string _gridName = "44_44";
    string _gridType = "A";
    GameObject _gridGO;
    //Seed to run the population method
    int _popSeed = 24;

    float _voxelSize = 0.375f;

    #endregion

    #region Grid data and objects collections

    List<Part> _existingParts = new List<Part>();
    List<PPSpace> _spaces = new List<PPSpace>();
    List<Voxel> _boundaries = new List<Voxel>();
    List<Tenant> _tenants = new List<Tenant>();
    List<PPSpaceRequest> _spaceRequests = new List<PPSpaceRequest>();
    List<ReconfigurationRequest> _reconfigurationRequests = new List<ReconfigurationRequest>();

    #endregion

    #region Simulation parameters
    int _frame = 0;

    int _day = 0;
    int _hour = 0;
    float _hourStep = 0.05f; //in seconds, represents a virtual hour
    bool _timePause;
    string _dateTimeNow;
    string[] _weekdaysNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
    int _currentWeekDay = 0;

    bool _progressionRunning = false;

    #endregion

    #region Debugging

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

    #endregion

    #endregion

    #region Unity Methods

    void Start()
    {
        _cam = Camera.main;

        _gridSize = new Vector3Int(32, 1, 24);
        MainGrid = new VoxelGrid(_gridSize, _voxelSize, transform.position, true);
        _paralellGrid = new VoxelGrid(MainGrid);
        _boundaries = MainGrid.Boundaries;
        _gridGO = MainGrid.GridGO;
        _gridGO.transform.SetParent(transform);

        //Load tenants and requests data
        _tenants = JSONReader.ReadTenantsWithPreferences("Input Data/U_TenantPreferences", MainGrid);
        _spaceRequests = JSONReader.ReadSpaceRequests("Input Data/U_SpaceRequests", _tenants);
        _cameraPivot.position = new Vector3(MainGrid.Size.x / 2, 0, MainGrid.Size.z / 2) * _voxelSize;

        //Create Configurable Parts
        PopulateRandomConfigurables(5);
        AnalyzeGridCreateNewSpaces();
        
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

        if (_selectedSpace != null)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SetSpaceToReconfigure();
            }
        }

        DrawActiveComponent();
        //StartCoroutine(SaveScreenshot());
    }

    #endregion

    #region Architectural functions and methods

    /// <summary>
    /// Runs the analyze, creating new spaces
    /// </summary>
    public void AnalyzeGridCreateNewSpaces()
    {
        MainGrid.RunAnalysisCreateNewSpaces();
        _boundaries = MainGrid.Boundaries;
        _spaces = MainGrid.Spaces;
    }

    /// <summary>
    /// Analyzes the current state of the grid, attempting to keep and update the spaces
    /// that can be understood as the same
    /// </summary>
    public void AnalyzeGridUpdateSpaces()
    {
        MainGrid.RunAnalysisUpdateSpaces();
        _boundaries = MainGrid.Boundaries;
        _spaces = MainGrid.Spaces;
        //CheckReconfigurationResults(); //This checks the results for every request
    }

    /// <summary>
    /// Populates a given number of configurable parts on the grid
    /// </summary>
    /// <param name="amt">The amount of parts to create</param>
    private void PopulateRandomConfigurables(int amt)
    {
        for (int i = 0; i < amt; i++)
        {
            string partName = $"CP_{i}";
            bool success = false;
            ConfigurablePart p = new ConfigurablePart();
            int attempt = 0;
            while (!success)
            {
                p = new ConfigurablePart(MainGrid, !_showVoxels, _popSeed + attempt, partName, out success);
                attempt++;
            }
            MainGrid.ExistingParts.Add(p);
            _existingParts.Add(p);
        }
    }

    /// <summary>
    /// UPDATE THIS TO RUN ON EVIRONMENT Gets the space that the Arrow object represents 
    /// </summary>
    /// <returns>The PPSpace object</returns>
    private PPSpace GetSpaceFromArrow()
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

    /// <summary>
    /// Checks the results of the latest reconfiguration against the requests that were made
    /// </summary>
    public void CheckReconfigurationResults()
    {
        //foreach (var request in _reconfigurationRequests)
        for (int i = 0; i < _reconfigurationRequests.Count; i++)
        {
            var request = _reconfigurationRequests[i];
            Guid spaceId = request.SpaceId;
            PPSpace space = MainGrid.GetSpaceById(spaceId);
            if (space != null)
            {
                bool success = request.ReconfigurationSuccessful(space);
                if (success)
                {
                    //print($"{space.Name} reconfiguration was successful. wanted {request.TargetArea}, got {space.VoxelCount}");
                    space.Reconfigure_Area = false;
                    space.Reconfigure_Connectivity = false;

                    _reconfigurationRequests.Remove(request);
                }
                else
                {
                    //print($"{space} reconfiguration was not successful. wanted {request.TargetArea}, got {space.VoxelCount}");

                }
            }
            else
            {
                //Currently not allowing the space to be destroyed, so actions are being undone
                //request.OnSpaceDestruction();
                //_reconfigurationRequests.Remove(request);
                //print($"{request.SpaceName} was destroyed.");
            }
        }
    }

    /// <summary>
    /// Checks if the reconfiguration subject of a request is still valid
    /// or has been destroyed
    /// </summary>
    /// <param name="request">The request to be assessed</param>
    /// <returns>The validity of the reconfiguration</returns>
    public bool CheckResultFromRequest(ReconfigurationRequest request)
    {
        bool result = true;
        Guid spaceId = request.SpaceId;
        int checkCount = _spaces.Count(s => s.SpaceId == spaceId);
        if (checkCount == 1)
        {
            PPSpace space = MainGrid.GetSpaceById(spaceId);
            if (space != null)
            {
                //Space still exists, evaluate if reconfiguration was successful
                bool success = request.ReconfigurationSuccessful(space);
                if (success)
                {
                    print($"{space.Name} reconfiguration was successful. wanted {request.TargetArea}, got {space.VoxelCount}");
                    space.Reconfigure_Area = false;
                    space.Reconfigure_Connectivity = false;
                    _reconfigurationRequests.Remove(request);
                }
                else
                {
                    print($"{space} reconfiguration was not successful. wanted {request.TargetArea}, got {space.VoxelCount}");

                }
            }
        }
        else if (checkCount > 1)
        {
            print($"{request.SpaceName} was split.");
            //Space was destroyed and split into 2 or more. Differentiate new spaces
            foreach (var space in _spaces.Where(s => s.SpaceId == spaceId))
            {
                space.SpaceId = Guid.NewGuid();
                space.Reconfigure_Area = false;
                space.Reconfigure_Connectivity = false;
            }
            request.OnSpaceDestruction();
            _reconfigurationRequests.Remove(request);
            result = false;
        }
        else
        {
            //Space was destroyed, return false
            print($"{request.SpaceName} was destroyed.");
            request.OnSpaceDestruction();
            _reconfigurationRequests.Remove(request);
            result = false;
        }

        return result;
    }

    /// <summary>
    /// Forces reseting the Spaces list to a previous state, only to be used after undoing an action
    /// </summary>
    /// <param name="previousSpaces">The list to be used</param>
    public void ForceResetSpaces(List<PPSpace> previousSpaces)
    {
        _spaces = previousSpaces;
        //foreach (var space in _spaces)
        //{
        //    space.CreateArrow();
        //}
        MainGrid.ForceSpaceReset(previousSpaces);
    }

    /// <summary>
    /// Gets the current List of spaces on the environment
    /// </summary>
    /// <returns>The spaces as a List</returns>
    public List<PPSpace> GetCurrentSpaces()
    {
        return _spaces;
    }

    #endregion

    #region Space utilization functions and methods

    /// <summary>
    /// Manually sets a space to be reconfigured
    /// </summary>
    private void SetSpaceToReconfigure()
    {
        ReconfigurationRequest rr = new ReconfigurationRequest(_selectedSpace, 1, 0);
        _reconfigurationRequests.Add(rr);
        _selectedSpace.ArtificialReconfigureRequest(0, 1);
    }

    /// <summary>
    /// IEnumerator to run the daily progression of the occupation simulation
    /// </summary>
    /// <returns></returns>
    private IEnumerator DailyProgression()
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
                float hourProbability = UnityEngine.Random.value;
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
    private void CheckSpaces()
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
    private void CheckForReconfiguration()
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
    private void RequestSpace(PPSpaceRequest request)
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
    private void NextHour()
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

    #endregion

    #region Drawing and Visualizing

    IEnumerator SaveScreenshot()
    {
        string file = $"SavedFrames/ReconfigurationTest/Frame_{_frame}.png";
        ScreenCapture.CaptureScreenshot(file);
        _frame++;
        yield return new WaitForEndOfFrame();
    }

    /// <summary>
    /// Change the visibility of the scene's GameObjects, iterating between 
    /// voxel and GameObject visualization
    /// </summary>
    /// <param name="visible">The boolean trigger</param>
    private void SetGameObjectsVisibility(bool visible)
    {
        var configurables = _existingParts.OfType<ConfigurablePart>().ToArray();
        if (configurables.Length > 0)
        {
            foreach (var c in configurables)
            {
                c.SetGOVisibility(!visible);
            }
        }
        //_gridGO.SetActive(!visible);
        MainGrid.SetGOVisibility(!visible);
    }

    /// <summary>
    /// Draws the current VoxelGrid state with mesh voxels
    /// </summary>
    private void DrawState()
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
                    if (MainGrid.Voxels[x, y, z].IsActive)
                    {
                        //PP_Drawing.DrawCube(transform.position + _grid.Voxels[x, y, z].Center, _grid.VoxelSize, 0);
                        PP_Drawing.DrawCube(transform.position + index, MainGrid.VoxelSize, Color.grey);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Draws the boundary voxels with meshes
    /// </summary>
    private void DrawBoundaries()
    {
        foreach (var voxel in _boundaries)
        {
            //Vector3 index = new Vector3(voxel.Index.x, voxel.Index.y, voxel.Index.z) * _voxelSize;
            Vector3 index = new Vector3(voxel.Index.x + 0.5f, voxel.Index.y + 0.5f, voxel.Index.z + 0.5f) * _voxelSize;
            //PP_Drawing.DrawCubeTransparent(transform.position + voxel.Center + new Vector3(0f, _voxelSize, 0f), _voxelSize);
            PP_Drawing.DrawCubeTransparent(transform.position + index + new Vector3(0f, _voxelSize, 0f), _voxelSize);
        }
    }

    /// <summary>
    /// Represents the spaces with voxel meshes
    /// </summary>
    private void DrawSpaces()
    {
        foreach (var space in MainGrid.Spaces)
        {
            Color color;
            Color black = Color.black;
            Color white = Color.white;
            Color acid = new Color(0.85f, 1.0f, 0.0f, 0.70f);
            if (space.Reconfigure)
            {
                if (space != _selectedSpace)
                {
                    //color = new Color(0.7f, 0.1f, 0.1f, 0.70f);
                    color = acid;
                }
                else
                {
                    //color = new Color(0.90f, 0.70f, 0.0f, 0.70f);
                    color = acid;
                }
            }
            else
            {
                if (space != _selectedSpace)
                {
                    //color = new Color(0.9f, 0.9f, 0.9f, 0.70f);
                    color = black;
                }
                else
                {
                    //color = new Color(0.85f, 1.0f, 0.0f, 0.70f);
                    color = black;
                }
            }
            PP_Drawing.DrawSpaceBoundary(space, MainGrid, color, transform.position);
        }
    }

    /// <summary>
    /// Draws the space tags
    /// </summary>
    private void DrawSpaceTags()
    {
        if (_showSpaces)
        {
            float tagHeight = 3.0f;
            Vector2 tagSize = new Vector2(60, 15);
            foreach (var space in MainGrid.Spaces)
            {
                string spaceName = space.Name;
                Vector3 tagWorldPos = transform.position + space.GetCenter() + (Vector3.up * tagHeight);

                var t = _cam.WorldToScreenPoint(tagWorldPos);
                Vector2 tagPos = new Vector2(t.x - (tagSize.x / 2), Screen.height - t.y);

                GUI.Box(new Rect(tagPos, tagSize), spaceName, "spaceTag");
            }
        }
    }

    /// <summary>
    /// Draws a box in the pivot point of the voxel configurable part
    /// </summary>
    private void DrawPartPivot()
    {
        foreach (var part in _existingParts)
        {
            float x = part.ReferenceIndex.x  * _voxelSize;
            float y = part.ReferenceIndex.y * _voxelSize;
            float z = part.ReferenceIndex.z * _voxelSize;
            Vector3 origin = new Vector3(x, y + 7*_voxelSize, z) ;
            PP_Drawing.DrawCube(origin, _voxelSize * 1.1f, 0.2f);
        }
    }

    /// <summary>
    /// Visual aid to show which ConfigurablePart is being controlled now
    /// </summary>
    private void DrawActiveComponent()
    {
        var unfrozenParts = _existingParts.OfType<ConfigurablePart>().Where(p => p.CPAgent.Frozen == false);
        foreach (var up in unfrozenParts)
        {
            var pos = transform.position + (new Vector3(up.Center.x + 0.5f, up.Center.y + 0.5f, up.Center.z + 0.5f) * _voxelSize);
            PP_Drawing.DrawCube(pos + new Vector3(0,4f,0), 0.25f, 0f);
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
            "Update Generate Spaces [AI]"))
        {
            AnalyzeGridCreateNewSpaces();
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

    /// <summary>
    /// Debug Window
    /// </summary>
    /// <param name="windowID">The Id to be called</param>
    private void DebugWindow(int windowID)
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
            //_debugMessage = _selectedSpace.GetSpaceInfo();
            _debugMessage = _spaceData;
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

    #endregion
}