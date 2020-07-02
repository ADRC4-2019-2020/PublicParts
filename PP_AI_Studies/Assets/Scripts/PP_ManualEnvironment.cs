using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Diagnostics;
using System;
using System.IO.Abstractions;
using UnityEngine.UI;

public class PP_ManualEnvironment : PP_Environment
{
    #region Fields and properties

    #region UI amd HUD
    
    public Text MessageBanner;
    public Text PreviousMessages;
    private string[] _messageStack = new string[6];
    public Text DataDisplay;
    public Text DayTimeDisplay;
    //public Text DisplayRequestTotal;
    private int _requestTotal = 0;
    public GameObject SpaceDataPanel;
    public GameObject TenantsDataPanel;
    public GameObject RequestsDataPanel;

    private Sprite _regularBorder;
    private Sprite _activeBorder;

    private Dictionary<PPSpace, string> _spacesMessages;
    
    #endregion

    #region Manual environment specific

    private ConfigurablePartAgent _selectedComponent;
    private MainCamera _camControl;

    private float _colorOscilator = 0;

    #endregion

    public ScreenRecorder ScreenRecorderInstance;

    #endregion

    #region Unity methods

    /// <summary>
    /// Configures the Environment, based on 
    /// </summary>
    private void Awake()
    {
        _nComponents = 5;
        _voxelSize = 0.375f;
        _existingParts = new List<Part>();
        _spaces = new List<PPSpace>();
        _boundaries = new List<Voxel>();
        _tenants = new List<Tenant>();
        _spaceRequests = new List<PPSpaceRequest>();
        _reconfigurationRequests = new List<ReconfigurationRequest>();

        _hourStep = 1f;
        InitializedAgents = 0;
        _showDebug = true;
        _compiledMessage = new string[2];
        _showRawBoundaries = false;
        _showSpaces = true;
        _showSpaceData = false;
        _showVoxels = false;
        _activityLog = "";
        _saveImageSteps = false;
        _timePause = true;

        _regularBorder = Resources.Load<Sprite>("Textures/RectangularBorder");
        _activeBorder = Resources.Load<Sprite>("Textures/RectangularBorder_Active");
    }

    private void Start()
    {
        _cam = Camera.main;
        _camControl = _cam.transform.GetComponent<MainCamera>();
        _camControl.Navigate = _timePause;
        

        _gridSize = new Vector3Int(30, 1, 24);
        MainGrid = new VoxelGrid(_gridSize, _voxelSize, transform.position, true, false);
        _boundaries = MainGrid.Boundaries;
        _gridGO = MainGrid.GridGO;
        _gridGO.transform.SetParent(transform);

        _cameraPivot.position = MainGrid.Origin + (new Vector3(_gridSize.x / 2f, _gridSize.y, _gridSize.z / 2f) * _voxelSize);

        //Load tenants and requests data
        _tenants = JSONReader.ReadTenantsWithPreferences("Input Data/U_TenantPreferences", MainGrid);
        _spaceRequests = JSONReader.ReadSpaceRequests("Input Data/U_SpaceRequests", _tenants);
        InitializeTenantDisplay();

        //Create Configurable Parts
        PopulateRandomConfigurables(_nComponents);
        AnalyzeGridCreateNewSpaces();
        RenewSpacesMesseges();
        SendSpacesData();
        StartCoroutine(DailyProgression());
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

        #region Control inputs

        if (Input.GetMouseButtonDown(0))
        {
            ActivateComponent();
        }

        if (Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            _hourStep += 0.05f;
            _hourStep = Mathf.Clamp(_hourStep, 0.05f, 1f);
            print(_hourStep);
        }
        if (Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            _hourStep -= 0.05f;
            _hourStep = Mathf.Clamp(_hourStep, 0.05f, 1f);
            print(_hourStep);
        }

        //Manual pause trigger
        if (Input.GetKeyDown(KeyCode.P))
        {
            _timePause = !_timePause;
            _camControl.Navigate = _timePause;
        }

        //Reconfigure and continue
        if (Input.GetKeyDown(KeyCode.Space))
        {
            //FinilizeReconfiguration();
            StartCoroutine(FinilizeReconfigurationAnimated());
        }

        #endregion

        //Oscilate the intensity of the colors used to represent the space
        if (!_timePause)  _colorOscilator = ((Mathf.Sin(Time.time * 3f) + 1) / 2f);
    }

    #endregion

    #region Architectural functions and methods
    
    /// <summary>
    /// Uses the mouse pointer to activate a component to be moved
    /// </summary>
    /// <returns></returns>
    private ConfigurablePartAgent ActivateComponent()
    {
        //This method allows clicking on the InfoArrow
        //and returns its respective space
        ConfigurablePartAgent clicked = null;
        Ray ClickRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ClickRay, out hit))
        {
            if (hit.collider.gameObject.transform != null && hit.collider.gameObject.tag == "ConfigurablePart")
            {
                //Clear the current active component
                if (_selectedComponent != null)
                {
                    _selectedComponent.FreezeAgent();
                    _selectedComponent = null;
                }
                clicked = hit.collider.gameObject.GetComponent<ConfigurablePartAgent>();
                _selectedComponent = clicked;
                _selectedComponent.UnfreezeAgent();
            }
            else
            {
                //Clear the current active component
                if (_selectedComponent != null)
                {
                    _selectedComponent.FreezeAgent();
                    _selectedComponent = null;
                }
            }
        }
        return clicked;
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

    private void FinilizeReconfiguration()
    {
        AnalyzeGridUpdateSpaces();
        SendSpacesData();
        ClearAllRequests();
        RenewSpacesMesseges();
        _timePause = false;
        _camControl.Navigate = _timePause;
    }

    private IEnumerator FinilizeReconfigurationAnimated()
    {
        var parts = _existingParts.OfType<ConfigurablePart>();
        var agents = parts.Select(p => p.CPAgent);
        foreach (var agent in agents)
        {
            agent.PrepareAnimation();
            while (agent.IsMoving)
            {
                agent.AnimateTransition(_hourStep);
                yield return null;

            }
        }

        AnalyzeGridUpdateSpaces();
        SendSpacesData();
        ClearAllRequests();
        RenewSpacesMesseges();
        _timePause = false;
        _camControl.Navigate = _timePause;
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

                DayTimeDisplay.text = $"Day {_day.ToString("D3")}" + " | " +
                    $"{_weekdaysNames[_currentWeekDay]}" + " | " +
                    $"{_hour}:00";

                var occupiedSpaces = _spaces.Where(s => s.Occupied);
                foreach (var space in occupiedSpaces)
                {
                    int[] useReturn = space.UseSpaceGetFeedback();
                    if (useReturn != null)
                    {
                        //_activityLog = useReturn;
                        //MessageBanner.text = useReturn;
                        //AddDisplayMessage(useReturn);

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
                        if (rProbability >= hourProbability)
                        {
                            RequestSpace(request);
                            _requestTotal++;
                        }
                    }
                }
                
                //DisplayRequestTotal.text = $"Total of space requests: {_requestTotal.ToString("D4")}";
                UpdateTenantDisplay();
                SendSpacesData();
                //SendReconfigureData();
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
            //string newMessage = $"Assinged {bestSuited.Name} to {request.Tenant.Name} at {_hour.ToString("D2")}:00 for {request.ActivityName}";
            string newMessage = $"Assinged to {request.Tenant.Name}";
            _spacesMessages[bestSuited] = newMessage;


            //AddDisplayMessage(newMessage);
        }
        else
        {
            //AddDisplayMessage("No available space found");
        }
        
    }

    /// <summary>
    /// Check if spaces need to be reconfigured
    /// </summary>
    protected override void CheckSpaces()
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
    protected override void CheckForReconfiguration()
    {
        if (_spaces.Count(s => s.Reconfigure) >= 1)
        {
            //AddDisplayMessage("Reconfiguration requested");
            //SendReconfigureData();

            _timePause = true;
            _camControl.Navigate = _timePause;

            var parts = _existingParts.OfType<ConfigurablePart>();
            var agents = parts.Select(p => p.CPAgent);
            foreach (var agent in agents)
            {
                agent.StoreInitialPosition();
            }
        }
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

    #endregion

    #region Drawing and representing

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
                        Vector2 messageTagPos = nameTagPos + new Vector2(+ nameTagSize.x/2 - messageTagSize.x/2, - messageTagSize.y - 4);
                        tagText = spaceMessage;
                        GUI.Box(new Rect(messageTagPos, messageTagSize), tagText, "assignedTag");

                    }
                    else if (spaceMessage.Contains("Bad"))
                    {
                        Vector2 messageTagSize = new Vector2(22, 22);
                        Vector2 messageTagPos = nameTagPos + new Vector2(+nameTagSize.x / 2 - messageTagSize.x / 2, -messageTagSize.y - 4);
                        tagText = ":(";
                        GUI.Box(new Rect(messageTagPos, messageTagSize), tagText, "badTag");
                    }
                    else if (spaceMessage.Contains("Good"))
                    {
                        Vector2 messageTagSize = new Vector2(22, 22);
                        Vector2 messageTagPos = nameTagPos + new Vector2(+nameTagSize.x / 2 - messageTagSize.x / 2, -messageTagSize.y - 4);
                        tagText = ":)";
                        GUI.Box(new Rect(messageTagPos, messageTagSize), tagText, "goodTag");
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
                Color black = Color.black;
                Color white = Color.white;
                //Color acid = new Color(0.85f, 1.0f, 0.0f, _areaTransparency);
                Color acid = new Color(0.85f * _colorOscilator, 1.0f * _colorOscilator, 0.0f * _colorOscilator, 0.5f);
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

    #region UI Controls and Settings

    /// <summary>
    /// Sends space data to be displayed on the UI (based on text elements docked on panel)
    /// </summary>
    private void SendSpacesData()
    {
        var spaces = _spaces.Where(s => !s.IsSpare).ToArray();

        int panelCount = SpaceDataPanel.transform.childCount;
        int spaceCount = spaces.Length;
        for (int i = 0; i < panelCount; i++)
        {
            var panel = SpaceDataPanel.transform.GetChild(i);
            var name = panel.Find("Name").GetComponent<Text>();
            var data = panel.Find("Data").GetComponent<Text>();
            var reconfigBanner = panel.Find("ReconfigBanner");
            var reconfigText = reconfigBanner.Find("ReconfigText").GetComponent<Text>();

            if (i < spaceCount)
            {
                var space = spaces[i];
                //Enable border
                panel.GetComponent<Image>().enabled = true;

                //Enable text
                name.enabled = true;
                data.enabled = true;

                //Set text content
                name.text = space.Name;
                data.text = space.GetSpaceData();

                
                if (space.Reconfigure)
                {
                    reconfigBanner.gameObject.SetActive(true);

                    string areaResult = "";
                    string connectResult = "";

                    if (space.Reconfigure_Area)
                    {
                        if (space._areaIncrease > space._areaDecrease)
                        {
                            areaResult = "+A";
                        }
                        else
                        {
                            areaResult = "-A";
                        }
                    }

                    if (space.Reconfigure_Connectivity)
                    {
                        if (space._connectivityIncrease > space._connectivityDecrease)
                        {
                            connectResult = "+C";
                        }
                        else
                        {
                            connectResult = "-C";
                        }
                    }

                    string resultReconfig;
                    if (areaResult != "" && connectResult != "")
                    {
                        areaResult = areaResult + " | ";
                    }
                    resultReconfig = areaResult + connectResult;
                    reconfigText.text = resultReconfig;
                }
                else
                {
                    reconfigBanner.gameObject.SetActive(false);
                }

                
            }
            else
            {
                //Disable border
                panel.GetComponent<Image>().enabled = false;

                //Disable text
                name.enabled = false;
                data.enabled = false;

                reconfigBanner.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Sends space data to be displayed on the UI (based on text elements docked on panel)
    /// </summary>
    private void SendReconfigureData()
    {
        var spaces = _spaces.Where(s => s.Reconfigure).ToArray();

        int panelCount = RequestsDataPanel.transform.childCount;
        int spaceCount = spaces.Length;
        for (int i = 0; i < panelCount; i++)
        {
            var panel = RequestsDataPanel.transform.GetChild(i).GetComponent<Text>();
            if (i < spaceCount)
            {
                var space = spaces[i];
                panel.enabled = true;
                panel.transform.GetChild(0).gameObject.SetActive(true);
                panel.text = spaces[i].GetReconfigurationData();
            }
            else
            {
                panel.transform.GetChild(0).gameObject.SetActive(false);
                panel.enabled = false;
            }
        }
    }

    /// <summary>
    /// Add messeges to be displyed on the UI, populating a message stack
    /// </summary>
    /// <param name="newMessage"></param>
    private void AddDisplayMessage(string newMessage)
    {
        _messageStack[0] = MessageBanner.text;
        MessageBanner.text = newMessage;
        for (int i = _messageStack.Length - 1; i > 0; i--)
        {
            _messageStack[i] = _messageStack[i - 1];
        }

        //string result = string.Concat(
        //    _messageStack
        //    .Select(m => m + "\n").Reverse());
        string result = string.Join("\n",_messageStack.Reverse());
        
        PreviousMessages.text = result;
    }

    /// <summary>
    /// Initialize the contents of the tenant display panel with the Tenant's data
    /// </summary>
    private void InitializeTenantDisplay()
    {
        int panelCount = TenantsDataPanel.transform.childCount;
        if (panelCount != _tenants.Count)
        {
            UnityEngine.Debug.Log("Tenant count does not match panel count!");
            return;
        }
        for (int i = 0; i < panelCount; i++)
        {
            var panel = TenantsDataPanel.transform.GetChild(i);
            Tenant tenant = _tenants[i];
            var name = panel.Find("TenantName").GetComponent<Text>();
            var status = panel.Find("Status").GetComponent<Text>();
            var activity = panel.Find("Activity").GetComponent<Text>();
            var border = panel.Find("Border").GetComponent<Image>();
            
            var image = panel.GetComponent<Image>();
            status.text = "";
            name.text = tenant.Name;
            activity.text = "";
            border.sprite = _regularBorder;
            //image.sprite = Resources.Load<Sprite>("Textures/" + tenant.Name.Split(' ')[0]);
        }
    }

    /// <summary>
    /// Update the contents of the tenant display panel with the Tenant's data
    /// </summary>
    private void UpdateTenantDisplay()
    {
        int panelCount = TenantsDataPanel.transform.childCount;

        for (int i = 0; i < panelCount; i++)
        {
            var panel = TenantsDataPanel.transform.GetChild(i);
            Tenant tenant = _tenants[i];
            var status = panel.Find("Status").GetComponent<Text>();
            var border = panel.Find("Border").GetComponent<Image>();
            var activity = panel.Find("Activity").GetComponent<Text>();

            if (tenant.OnSpace != null)
            {
                status.text = "@ " + tenant.OnSpace.Name;
                activity.text = tenant.OnSpace.GetTenantActivity();
                border.sprite = _activeBorder;
            }
            else
            {
                status.text = "";
                activity.text = "";
                border.sprite = _regularBorder;
            }
        }
    }

    private void OnGUI()
    {
        GUI.skin = _skin;
        GUI.depth = 2;

        //Draw Spaces tags
        DrawSpaceTags();

        //Draw spaces messages
        //DrawSpaceMessage();
    }

    #endregion
}
