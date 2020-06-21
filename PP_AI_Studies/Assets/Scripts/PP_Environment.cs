using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Diagnostics;
using System;

/// <summary>
/// Class to manage the environment in which a simulation occurs
/// </summary>
public class PP_Environment : MonoBehaviour
{
    #region Fields and Parameters

    //Object inputs
    [SerializeField] GUISkin _skin;
    [SerializeField] Transform _cameraPivot;
    Camera _cam;
    VoxelGrid _grid;
    Vector3Int _gridSize;

    #endregion

    #region Grid setup

    //Currently available slabs: 44_44_A
    string _gridName = "44_44";
    string _gridType = "A";
    GameObject _gridGO;
    //Seed to run the population method
    int _popSeed = 5;

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

    #region Unity Methods

    void Start()
    {
        _cam = Camera.main;

        //CreateGridFromFile();
        //_grid = new VoxelGrid(_gridName, _gridType, _voxelSize, transform.position);
        //_gridSize = _grid.Size;
        _gridSize = new Vector3Int(24, 1, 12);
        _grid = new VoxelGrid(_gridSize, _voxelSize, transform.position, true);
        _boundaries = _grid.Boundaries;
        _gridGO = _grid.GridGO;
        _gridGO.transform.SetParent(transform);

        //Load tenants and requests data
        _tenants = JSONReader.ReadTenantsWithPreferences("Input Data/U_TenantPreferences", _grid);
        _spaceRequests = JSONReader.ReadSpaceRequests("Input Data/U_SpaceRequests", _tenants);
        _cameraPivot.position = new Vector3(_grid.Size.x / 2, 0, _grid.Size.z / 2) * _voxelSize;

        //Create Configurable Parts
        CreateConfigurable(new Vector3Int(8, 0, 8), 1, "CP_A");
        CreateConfigurable(new Vector3Int(18, 0, 1), 3, "CP_B");
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

    }

    #endregion

    #region Architectural functions and methods

    /// <summary>
    /// Runs the populate and analyze method from the <see cref="VoxelGrid"/> 
    /// </summary>
    private void ExecutePopAndAnalysisOnGrid()
    {
        _grid.RunPopulationAndAnalysis();
        _existingParts = _grid.ExistingParts;
        _boundaries = _grid.Boundaries;
        _spaces = _grid.Spaces;
        //SetParentForNewConfigurables();
    }

    /// <summary>
    /// Runs the analyze, creating new spaces
    /// </summary>
    public void AnalyzeGridCreateNewSpaces()
    {
        _grid.RunAnalysisCreateNewSpaces();
        _boundaries = _grid.Boundaries;
        _spaces = _grid.Spaces;
    }

    /// <summary>
    /// Analyzes the current state of the grid, attempting to keep and update the spaces
    /// that can be understood as the same
    /// </summary>
    public void AnalyzeGridUpdateSpaces()
    {
        _grid.RunAnalysisUpdateSpaces();
        _boundaries = _grid.Boundaries;
        _spaces = _grid.Spaces;
        CheckReconfigurationResults();
    }

    /// <summary>
    /// Creates a configurable part in the selected origin with the set rotation
    /// </summary>
    /// <param name="origin">Origin to place the ReferenceIndex</param>
    /// <param name="rotation">Rotation to be applied</param>
    private void CreateConfigurable(Vector3Int origin, int rotation, string name)
    {
        ConfigurablePart p = new ConfigurablePart(_grid, origin, rotation, !_showVoxels, name, out bool success);
        if (success)
        {
            _grid.ExistingParts.Add(p);
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
    /// Checks the results of the latest reconfiguration against the requests that were made
    /// </summary>
    public void CheckReconfigurationResults()
    {
        //foreach (var request in _reconfigurationRequests)
        for (int i = 0; i < _reconfigurationRequests.Count; i++)
        {
            var request = _reconfigurationRequests[i];
            Guid spaceId = request.SpaceId;
            PPSpace space = _spaces.First(s => s.SpaceId == spaceId);
            if (space != null)
            {
                bool success = request.ReconfigurationSuccessful(space);
                if (success)
                {
                    print($"{space.Name} reconfiguration was successful. wanted {request.TargetArea}, got {space.VoxelCount}");
                    space.Reconfigure_Area = false;
                    space.Reconfigure_Connectivity = false;
                    foreach (var part in space.BoundaryParts)
                    {
                        part.CPAgent.FreezeAgent();
                    }
                    _reconfigurationRequests.Remove(request);
                }
                else
                {
                    print($"{space} reconfiguration was not successful. wanted {request.TargetArea}, got {space.VoxelCount}");
                    foreach (var part in space.BoundaryParts)
                    {
                        part.CPAgent.UnfreezeAgent();
                    }
                }
            }
            else
            {
                //MUST DO SOMETHING HERE WITH THE SPACE WAS DESTROYED, POSSIBLY FREEZE THE AGENTS THROUGH THE REQUEST
            }
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
        _grid.SetGOVisibility(!visible);
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
                    if (_grid.Voxels[x, y, z].IsOccupied)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            var voxel = _grid.Voxels[x, y, z];
                            if (voxel.Part.Type == PartType.Configurable)
                            {
                                //PP_Drawing.DrawConfigurable(transform.position + _grid.Voxels[x, y, z].Center + new Vector3(0, (i + 1) * _voxelSize, 0), _grid.VoxelSize, 1);
                                PP_Drawing.DrawConfigurable(transform.position + index + new Vector3(0, (i + 1) * _voxelSize, 0), _grid.VoxelSize, 1);
                            }
                            else
                            {
                                //PP_Drawing.DrawCube(transform.position +  _grid.Voxels[x, y, z].Center + new Vector3(0, (i + 1) * _voxelSize, 0), _grid.VoxelSize, 1);
                                PP_Drawing.DrawCube(transform.position + index + new Vector3(0, (i + 1) * _voxelSize, 0), _grid.VoxelSize, 1);
                            }

                        }

                    }
                    if (_grid.Voxels[x, y, z].IsActive)
                    {
                        //PP_Drawing.DrawCube(transform.position + _grid.Voxels[x, y, z].Center, _grid.VoxelSize, 0);
                        PP_Drawing.DrawCube(transform.position + index, _grid.VoxelSize, 0);
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
        foreach (var space in _grid.Spaces)
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
            PP_Drawing.DrawSpaceBoundary(space, _grid, color, transform.position);
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
            foreach (var space in _grid.Spaces)
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