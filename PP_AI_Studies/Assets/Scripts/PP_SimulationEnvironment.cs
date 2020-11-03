using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using System.Globalization;
using Unity.Barracuda;

public class PP_SimulationEnvironment : PP_Environment
{
    #region Fields and Properties

    public GUISkin _skin;
    //public Text MessageBanner;
    //private string[] _messageStack = new string[6];
    //public Text DayTimeDisplay;
    //public Text SpeedDisplay;
    private int _requestTotal = 0;
    //public GameObject SpaceDataPanel;
    //public GameObject TenantsDataPanel;

    //private Sprite _regularBorder;
    //private Sprite _activeBorder;

    private Dictionary<PPSpace, string> _spacesMessages;

    private float _colorOscilator = 0;

    private ReconfigurationRequest _activeRequest;

    #region Simulation External Parameters

    private bool _drawGraphics;
    public string TenantFile;

    #endregion

    #endregion

    #region Unity Methods

    private void Awake()
    {
        _nComponents = 5;
        _voxelSize = 0.375f;
        _existingParts = new List<Part>();
        _agents = new List<ConfigurablePartAgent>();
        _spaces = new List<PPSpace>();
        _boundaries = new List<Voxel>();
        _tenants = new List<Tenant>();
        _spaceRequests = new List<PPSpaceRequest>();
        _reconfigurationRequests = new List<ReconfigurationRequest>();

        _activeRequest = null;

        _hourStep = 0.05f;
        _hour = 8;

        InitializedAgents = 0;
        _showDebug = true;
        _compiledMessage = new string[2];
        _showRawBoundaries = false;
        _showSpaces = true;
        _showSpaceData = false;
        _showVoxels = false;
        _activityLog = "";
        _timePause = true;

        //_regularBorder = Resources.Load<Sprite>("Textures/RectangularBorder");
        //_activeBorder = Resources.Load<Sprite>("Textures/RectangularBorder_Active");

        _cam = Camera.main;
    }

    private void Start()
    {
        _gridSize = new Vector3Int(30, 1, 24);
        MainGrid = new VoxelGrid(_gridSize, _voxelSize, transform.position, true, false);
        _boundaries = MainGrid.Boundaries;
        _gridGO = MainGrid.GridGO;
        _gridGO.transform.SetParent(transform);

        //_cameraPivot.position = MainGrid.Origin + (new Vector3(_gridSize.x / 2f, _gridSize.y, _gridSize.z / 2f) * _voxelSize);

        //Load tenants and requests data
        _tenants = JSONReader.ReadTenantsWithPreferences($"Input Data/U_TenantPreferences {TenantFile}", MainGrid);
        _spaceRequests = JSONReader.ReadSpaceRequests("Input Data/U_SpaceRequests", _tenants);
        InitializeTenantDisplay();

        //Create Configurable Parts
        PopulateRandomConfigurables(_nComponents);
        PopulateAgentsList();
        AnalyzeGridCreateNewSpaces();
        //RenameSpaces();
        RenewSpacesMesseges();
        //SendSpacesData();
        StartCoroutine(DailyProgression());

        //UpdateSpeedDisplay();
        SetAreaModel();
    }

    private void Update()
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

        #region Control inputs

        if (Input.GetKeyDown(KeyCode.V))
        {
            _showVoxels = !_showVoxels;
            SetGameObjectsVisibility(_showVoxels);
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            _showRawBoundaries = !_showRawBoundaries;
        }

        //Slow down simulation
        //if (Input.GetKeyDown(KeyCode.KeypadMinus))
        //{
        //    _hourStep += 0.05f;
        //    _hourStep = Mathf.Clamp(_hourStep, 0.05f, 1f);
        //    //UpdateSpeedDisplay();
        //    //print(_hourStep);
        //}
        //Speed up simulation
        //if (Input.GetKeyDown(KeyCode.KeypadPlus))
        //{
        //    _hourStep -= 0.05f;
        //    _hourStep = Mathf.Clamp(_hourStep, 0.05f, 1f);
        //    //UpdateSpeedDisplay();
        //    //print(_hourStep);
        //}

        //Manual pause trigger
        //if (Input.GetKeyDown(KeyCode.P))
        //{
        //    _timePause = !_timePause;
        //}

        //Reconfigure and continue
        //if (Input.GetKeyDown(KeyCode.Space))
        //{
        //    FinilizeReconfiguration();
        //    //StartCoroutine(FinilizeReconfigurationAnimated());
        //}

        //if (Input.GetKeyDown(KeyCode.M))
        //{
        //    _hourStep = 0.025f;
        //    //UpdateSpeedDisplay();
        //}

        //if (Input.GetKeyDown(KeyCode.W))
        //{
        //    var ss = _spaces.Where(s => !s.IsSpare);
        //    foreach (var s in ss)
        //    {
        //        print($"{s.Name}: {s.NumberOfConnections}");
        //    }
        //}

        #endregion

        //Oscilate the intensity of the colors used to represent the space
        if (!_timePause)
        {
            _colorOscilator = ((Mathf.Sin(Time.time * 3) + 1) / 2f);
        }

        //Check if the reconfiguration request is finished
        //if (_timePause && _reconfigurationRequests.Count > 0 && _reconfigurationRequests[0] != null)
        if (_timePause && _activeRequest != null)
        {
            //print("Entered here");
            //var request = _reconfigurationRequests[0];
            if (_activeRequest.AllAgentsFinished())
            {
                FinilizeReconfiguration();
                if (_spaces.Where(s => s.SpaceId == _activeRequest.SpaceId).Count() != 0)
                {
                    var recSpace = _spaces.First(s => s.SpaceId == _activeRequest.SpaceId);
                    recSpace.ResetAreaEvaluation();
                    recSpace.ResetConnectivityEvaluation();
                    recSpace.TimesSurvived = 0;
                    recSpace.TimesUsed = 0;
                }
                _activeRequest = null;
                
                foreach (var agent in _agents)
                {
                    agent.FreezeAgent();
                    agent.ClearRequest();
                }
            }
            //Request the next action from the agents
            else
            {
                _activeRequest.RequestNextAction();
            }
        }
    }

    #endregion

    #region Space utilization functions and methods

    /// <summary>
    /// Manages the daily progression of the simulation
    /// </summary>
    /// <returns></returns>
    protected override IEnumerator DailyProgression()
    {
        while (_day < 365)
        {
            if (!_timePause)
            {
                //Clears spaces messages in the beggining of each iteration
                RenewSpacesMesseges();

                if (_hour % 12 == 0)
                {
                    CheckSpaces();
                    CheckForReconfiguration();
                }

                //DayTimeDisplay.text = $"Day {_day.ToString("D3")}" + " | " +
                //    $"{_weekdaysNames[_currentWeekDay]}" + " | " +
                //    $"{_hour}:00";

                var occupiedSpaces = _spaces.Where(s => s.Occupied);
                foreach (var space in occupiedSpaces)
                {
                    int[] useReturn = space.UseSpaceGetFeedback();
                    if (useReturn != null)
                    {
                        if (useReturn.Contains(0))
                        {
                            _spacesMessages[space] = "Feedback: Bad";
                        }
                        else if (useReturn[0] == 1 && useReturn[1] == 1)
                        {
                            _spacesMessages[space] = "Feedback Good";
                        }
                    }
                }

                float hourProbability = UnityEngine.Random.value;
                foreach (var request in _spaceRequests)
                {
                    if (request.StartTime == _hour)
                    {
                        var rProbability = request.RequestProbability[_currentWeekDay];
                        if (rProbability >= hourProbability && request.Tenant.OnSpace == null)
                        {
                            RequestSpace(request);
                            _requestTotal++;
                        }
                    }
                }

                //UpdateTenantDisplay();
                //SendSpacesData();
                NextHour();

                yield return new WaitForSeconds(_hourStep);
            }
            else
            {
                yield return null;
            }
        }
    }

    /// <summary>
    /// Manages the probabilistic simulation of Tenants requesting spaces
    /// </summary>
    /// <param name="request"></param>
    protected override void RequestSpace(PPSpaceRequest request)
    {
        var requestArea = request.Population * request.Tenant.AreaPerIndInferred; //Request area assuming the area the tenant prefers per individual
        var availableSpaces = _spaces.Where(s => !s.Occupied && !s.IsSpare);

        if (availableSpaces.Count() > 0)
        {
            PPSpace bestSuited = availableSpaces.MaxBy(s => s.Area);
            foreach (var space in availableSpaces)
            {
                var spaceArea = space.Area;

                if (spaceArea >= requestArea && spaceArea < bestSuited.Area)
                {
                    bestSuited = space;
                }
            }
            bestSuited.OccupySpace(request);
            string newMessage = $"Assinged to {request.Tenant.Name}";
            _spacesMessages[bestSuited] = newMessage;
        }
    }

    /// <summary>
    /// Check if spaces need to be reconfigured
    /// </summary>
    protected override void CheckSpaces()
    {
        foreach (var space in _spaces)
        {
            if (space.TimesUsed >= 15)
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
            //else if (space.TimesUsed == 0 && space.TimesSurvived > 20)
            //{
            //    space.Reconfigure_Area = true;
            //    space.ArtificialReconfigureRequest(-1, 0);
            //}
        }
    }

    /// <summary>
    /// Check if there are enough requests to initialize reconfiguration. If so, pauses simulation
    /// and tells the agents to store their current positions
    /// </summary>
    protected override void CheckForReconfiguration()
    {
        //If the space count (of non-spare spaces) is 0, restart the position of every other part
        if (_spaces.Where(s => !s.IsSpare).Count() == 1)
        {
            for (int i = 0; i < _agents.Count; i++)
            {
                if (i % 2 == 0)
                {
                    ConfigurablePart part = _agents[i].GetPart();
                    int attempt = 0;
                    bool success = false;
                    foreach (var index in part.OccupiedIndexes)
                    {
                        var voxel = MainGrid.Voxels[index.x, index.y, index.z];
                        voxel.IsOccupied = false;
                        voxel.Part = null;
                    }
                    while (!success)
                    {
                        part.JumpToNewPosition(PopSeed + attempt, out success);
                        attempt++;
                    }
                }
            }

            FinilizeReconfiguration();
            _activeRequest = null;
            foreach (var agent in _agents)
            {
                agent.FreezeAgent();
                agent.ClearRequest();
            }
            ResetSpacesEvaluation();

        }
        
        //If all ok, continue checking for reconfiguration
        else if (_spaces.Count(s => s.Reconfigure) > 0)
        {
            _timePause = true;
            //Get only the first one and only create one type of request.
            var toReconfigure = _spaces.Where(s => s.Reconfigure).ToList();
            var target = toReconfigure[0];
            if (target.Reconfigure_Area)
            {
                //SetAreaModel();
                int targetArea = 1;
                if (target._areaDecrease > target._areaIncrease) targetArea = -1;

                ReconfigurationRequest rr = new ReconfigurationRequest(target, targetArea, 0);
                _activeRequest = rr;
                //_reconfigurationRequests.Add(rr);
            }
            else if (target.Reconfigure_Connectivity)
            {
                //SetConnectivityModel();
                int targetConnec = 1;
                if (target._connectivityDecrease > target._connectivityIncrease) targetConnec = -1;

                ReconfigurationRequest rr = new ReconfigurationRequest(target, 0, targetConnec);
                _activeRequest = rr;
                //_reconfigurationRequests.Add(rr);
            }

            var spaces = _spaces.Where(s => s.Reconfigure).ToList();
            for (int i = 1; i < _spaces.Count(s => s.Reconfigure); i++)
            {
                PPSpace space = spaces[i];
                space.ResetAreaEvaluation();
                space.ResetConnectivityEvaluation();
                space.TimesSurvived = 0;
                space.TimesUsed = 0;
            }

        }
    }

    /// <summary>
    /// Update the contents of the tenant display panel with the Tenant's data
    /// </summary>
    private void UpdateTenantDisplay()
    {
        //int panelCount = TenantsDataPanel.transform.childCount;

        //for (int i = 0; i < panelCount; i++)
        //{
        //    var panel = TenantsDataPanel.transform.GetChild(i);
        //    Tenant tenant = _tenants[i];
        //    var status = panel.Find("Status").GetComponent<Text>();
        //    var border = panel.Find("Border").GetComponent<Image>();
        //    var activity = panel.Find("Activity").GetComponent<Text>();

        //    if (tenant.OnSpace != null)
        //    {
        //        status.text = "@ " + tenant.OnSpace.Name;
        //        activity.text = tenant.OnSpace.GetTenantActivity();
        //        border.sprite = _activeBorder;
        //    }
        //    else
        //    {
        //        status.text = "";
        //        activity.text = "";
        //        border.sprite = _regularBorder;
        //    }
        //}
    }

    /// <summary>
    /// Analyze the grid and update data without animating the process
    /// </summary>
    private void FinilizeReconfiguration()
    {
        //ClearAllRequests();
        _reconfigurationRequests = new List<ReconfigurationRequest>();
        RenewSpacesMesseges();
        AnalyzeGridUpdateSpaces();
        //SendSpacesData();
        _timePause = false;
    }

    /// <summary>
    /// Clears all reconfiguration requests for all spaces
    /// </summary>
    private void ClearAllRequests()
    {
        foreach (var space in _spaces)
        {
            if (space.Reconfigure_Area == true)
            {
                space.ResetAreaEvaluation();
            }
            if (space.Reconfigure_Connectivity)
            {
                space.ResetConnectivityEvaluation();
            }
        }
        //SendReconfigureData();
    }

    /// <summary>
    /// Set the agents to use the Area brain
    /// </summary>
    private void SetAreaModel()
    {
        NNModel model = Resources.Load<NNModel>("Models/ConfigurablePart_Area");
        foreach (var agent in _agents)
        {
            agent.SetModel("ConfigurablePart", model);
        }
    }

    /// <summary>
    /// Set the agents to use the Connectivity brain
    /// </summary>
    private void SetConnectivityModel()
    {
        NNModel model = Resources.Load<NNModel>("Models/ConfigurablePart_Connectivity");
        foreach (var agent in _agents)
        {
            agent.SetModel("ConfigurablePart", model);
        }
    }

    /// <summary>
    /// Restarts the slab configuration if the number of available spaces is of only 1
    /// </summary>
    private bool SafeRestart()
    {
        bool restarted = false;
        var availableSpaces = _spaces.Where(s => !s.Occupied && !s.IsSpare);
        if (availableSpaces.Count() == 1)
        {
            restarted = true;
        }

        return restarted;
    }

    /// <summary>
    /// Clears all the reconfiguration evaluation on the spaces
    /// reseting them to default.
    /// </summary>
    protected override void ResetSpacesEvaluation()
    {
        foreach (var space in _spaces)
        {
            space.ResetAreaEvaluation();
            space.ResetConnectivityEvaluation();
            space.TimesUsed = 0;
        }
    }

    #endregion

    #region Architectural methods

    /// <summary>
    /// Populates the list of agents after the components have been created
    /// </summary>
    private void PopulateAgentsList()
    {
        foreach (ConfigurablePart part in _existingParts)
        {
            _agents.Add(part.CPAgent);
        }
    }

    #endregion

    #region UI Controls and Settings

    /// <summary>
    /// Initialize the contents of the tenant display panel with the Tenant's data
    /// </summary>
    private void InitializeTenantDisplay()
    {
        //int panelCount = TenantsDataPanel.transform.childCount;
        //if (panelCount != _tenants.Count)
        //{
        //    UnityEngine.Debug.Log("Tenant count does not match panel count!");
        //    return;
        //}
        //for (int i = 0; i < panelCount; i++)
        //{
        //    var panel = TenantsDataPanel.transform.GetChild(i);
        //    Tenant tenant = _tenants[i];
        //    var name = panel.Find("TenantName").GetComponent<Text>();
        //    var status = panel.Find("Status").GetComponent<Text>();
        //    var activity = panel.Find("Activity").GetComponent<Text>();
        //    var border = panel.Find("Border").GetComponent<Image>();

        //    var image = panel.GetComponent<Image>();
        //    status.text = "";
        //    name.text = tenant.Name;
        //    activity.text = "";
        //    border.sprite = _regularBorder;
        //}
    }

    /// <summary>
    /// Renames the spaces that are not spare in an orderly fashion for display purposes
    /// </summary>
    private void RenameSpaces()
    {
        var ordered = _spaces.OrderBy(s => s.IsSpare);

        int count = 0;
        foreach (PPSpace space in ordered)
        {
            space.Name = $"Space_{count}";
            count++;
        }
    }

    /// <summary>
    /// Recreates the dictionary for the messages for each space after reconfiguration
    /// </summary>
    private void RenewSpacesMesseges()
    {
        _spacesMessages = new Dictionary<PPSpace, string>();
        foreach (PPSpace space in _spaces)
        {
            _spacesMessages.Add(space, "");
        }
    }

    /// <summary>
    /// Sends space data to be displayed on the UI (based on text elements docked on panel)
    /// </summary>
    private void SendSpacesData()
    {
        //var spaces = _spaces.Where(s => !s.IsSpare).ToArray();

        //int panelCount = SpaceDataPanel.transform.childCount;
        //int spaceCount = spaces.Length;
        //for (int i = 0; i < panelCount; i++)
        //{
        //    var panel = SpaceDataPanel.transform.GetChild(i);
        //    var name = panel.Find("Name").GetComponent<Text>();
        //    var data = panel.Find("Data").GetComponent<Text>();
        //    var reconfigBanner = panel.Find("ReconfigBanner");
        //    var reconfigText = reconfigBanner.Find("ReconfigText").GetComponent<Text>();

        //    if (i < spaceCount)
        //    {
        //        var space = spaces[i];
        //        //Enable border
        //        panel.GetComponent<Image>().enabled = true;

        //        //Enable text
        //        name.enabled = true;
        //        data.enabled = true;

        //        //Set text content
        //        name.text = space.Name;
        //        data.text = space.GetSpaceData();


        //        if (space.Reconfigure)
        //        {
        //            reconfigBanner.gameObject.SetActive(true);

        //            string areaResult = "";
        //            string connectResult = "";

        //            if (space.Reconfigure_Area)
        //            {
        //                if (space._areaIncrease > space._areaDecrease)
        //                {
        //                    areaResult = "+A";
        //                }
        //                else
        //                {
        //                    areaResult = "-A";
        //                }
        //            }

        //            if (space.Reconfigure_Connectivity)
        //            {
        //                if (space._connectivityIncrease > space._connectivityDecrease)
        //                {
        //                    connectResult = "+C";
        //                }
        //                else
        //                {
        //                    connectResult = "-C";
        //                }
        //            }

        //            string resultReconfig;
        //            if (areaResult != "" && connectResult != "")
        //            {
        //                areaResult = areaResult + " | ";
        //            }

        //            resultReconfig = areaResult + connectResult;

        //            if (!space.Reconfigure_Area && !space.Reconfigure_Connectivity && (space.TimesUsed == 0 && space.TimesSurvived > 5))
        //            {
        //                resultReconfig = "NO USE";
        //            }

        //            reconfigText.text = resultReconfig;
        //        }
        //        else
        //        {
        //            reconfigBanner.gameObject.SetActive(false);
        //        }


        //    }
        //    else
        //    {
        //        //Disable border
        //        panel.GetComponent<Image>().enabled = false;

        //        //Disable text
        //        name.enabled = false;
        //        data.enabled = false;

        //        reconfigBanner.gameObject.SetActive(false);
        //    }
        //}
    }

    /// <summary>
    /// Updates the formated speed display
    /// </summary>
    private void UpdateSpeedDisplay()
    {
        //SpeedDisplay.text = $"{(1f / _hourStep).ToString("F1", CultureInfo.InvariantCulture)} X";
    }

    #endregion

    #region Simulation control

    /// <summary>
    /// Externally sets the speed of the evironment
    /// </summary>
    /// <param name="speed"></param>
    public void SetEnvironmentSpeed(float speed)
    {
        _hourStep = speed;
    }

    /// <summary>
    /// Externally determine if the environment should be paused or not
    /// </summary>
    /// <param name="val">The value to set</param>
    public void PauseEnvironment(bool val)
    {
        _timePause = val;
    }

    /// <summary>
    /// Get the formated speed text to be used on the canvas
    /// </summary>
    /// <returns>The formated string</returns>
    public string GetSpeedText()
    {
        return $"{(1f / _hourStep).ToString("F1", CultureInfo.InvariantCulture)} X";
    }

    /// <summary>
    /// Get the formated time and date text to be used on the canvas
    /// </summary>
    /// <returns>The formated string</returns>
    public string GetDateTimeText()
    {
       return $"Day {_day.ToString("D3")}" + " | " +
            $"{_weekdaysNames[_currentWeekDay]}" + " | " +
        $"{_hour}:00";
    }

    #endregion

    #region UI graphics

    /// <summary>
    /// Set if spaces graphics should be drawn
    /// </summary>
    /// <param name="val">Boolean value to set</param>
    public void DrawGraphics(bool val)
    {
        _drawGraphics = val;
    }

    /// <summary>
    /// Draws the space tags
    /// </summary>
    protected override void DrawSpaceTags()
    {
        if (_showSpaces)
        {
            var spaces = _spaces.Where(s => !s.IsSpare).ToArray();
            float nameTagHeight = 3.0f;

            Vector2 nameTagSize = new Vector2(64, 22);

            foreach (var space in spaces)
            {
                string spaceName = space.Name;
                Vector3 tagWorldPos = transform.position + space.GetCenter() + (Vector3.up * nameTagHeight);

                var t = _cam.WorldToScreenPoint(tagWorldPos);
                Vector2 nameTagPos = new Vector2(t.x - (nameTagSize.x / 2), Screen.height - t.y);

                GUI.Box(new Rect(nameTagPos, nameTagSize), spaceName, "spaceTag");

                //Return if key is not present in dictionary
                if (!_spacesMessages.ContainsKey(space)) return;

                var spaceMessage = _spacesMessages[space];
                if (spaceMessage != "")
                {
                    string tagText;

                    if (spaceMessage[0] == 'A')
                    {
                        Vector2 messageTagSize = new Vector2(120, 22);
                        Vector2 messageTagPos = nameTagPos + new Vector2(+nameTagSize.x / 2 - messageTagSize.x / 2, -messageTagSize.y - 4);
                        tagText = spaceMessage;
                        GUI.Box(new Rect(messageTagPos, messageTagSize), tagText, "assignedTag");

                    }
                    else if (spaceMessage.Contains("Bad"))
                    {
                        Vector2 messageTagSize = new Vector2(24, 24);
                        Vector2 messageTagPos = nameTagPos + new Vector2(+nameTagSize.x / 2 - messageTagSize.x / 2, -messageTagSize.y - 4);
                        tagText = "";
                        GUI.Box(new Rect(messageTagPos, messageTagSize), tagText, "badTag");
                    }
                    else if (spaceMessage.Contains("Good"))
                    {
                        Vector2 messageTagSize = new Vector2(24, 24);
                        Vector2 messageTagPos = nameTagPos + new Vector2(+nameTagSize.x / 2 - messageTagSize.x / 2, -messageTagSize.y - 4);
                        tagText = "";
                        GUI.Box(new Rect(messageTagPos, messageTagSize), tagText, "goodTag");
                    }
                }
            }
        }
    }

    private void OnGUI()
    {
        GUI.skin = _skin;
        GUI.depth = 2;

        //Draw Spaces tags
        if (_drawGraphics)
        {
            DrawSpaceTags();
        }
    }

    #endregion

    #region Drawing and representation

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
                Color black = Color.black;
                Color white = Color.white;
                Color acid = new Color(0.85f, 1.0f, 0.0f, 0.75f);
                //Color acid = new Color(0.85f * _colorOscilator, 1.0f * _colorOscilator, 0.0f * _colorOscilator, 0.5f);
                //Color grey = new Color(0f, 0f, 0f, _areaTransparency);
                Color grey = new Color(_colorOscilator, _colorOscilator, _colorOscilator, 0.5f);
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

    #endregion
}
