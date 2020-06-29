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

    public GUISkin _skin;
    public Transform _cameraPivot;
    protected Camera _cam;
    protected VoxelGrid MainGrid;
    private VoxelGrid _paralellGrid;
    protected Vector3Int _gridSize;

    #endregion

    #region Grid setup

    protected GameObject _gridGO;
    //Seed to run the population method
    protected int[] _availableSeeds;
    public int PopSeed;
    protected int _nComponents;

    protected float _voxelSize;

    #endregion

    #region Grid data and objects collections

    protected List<Part> _existingParts;
    protected List<PPSpace> _spaces;
    protected List<Voxel> _boundaries;
    protected List<Tenant> _tenants;
    protected List<PPSpaceRequest> _spaceRequests;
    protected List<ReconfigurationRequest> _reconfigurationRequests;

    #endregion

    #region Simulation properties
    protected int _frame = 0;

    protected int _day = 0;
    protected int _hour = 0;
    protected float _hourStep; //in seconds, represents a virtual hour
    protected bool _timePause;
    protected string _dateTimeNow;
    protected string[] _weekdaysNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
    protected int _currentWeekDay = 0;

    protected Vector3Int[] _completedIndices;
    protected bool _showCompleted;
    protected Color _completedColor;

    #endregion

    #region MLAgents properties

    protected bool _progressionRunning = false;
    public int InitializedAgents = 0;

    #endregion

    #region Debugging

    protected bool _showDebug;
    protected string _debugMessage;
    protected string[] _compiledMessage;

    protected bool _showTime;
    protected bool _showRawBoundaries;
    protected bool _showSpaces;
    protected bool _showSpaceData;

    protected bool _showVoxels;

    protected string _outputMessage;
    protected string _spaceData;
    protected string _activityLog;

    protected PPSpace _selectedSpace;

    protected bool _saveImageSteps;

    #endregion

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
    protected virtual void PopulateRandomConfigurables(int amt)
    {
        for (int i = 0; i < amt; i++)
        {
            string partName = $"CP_{i}";
            bool success = false;
            ConfigurablePart p = new ConfigurablePart();
            int attempt = 0;
            while (!success)
            {
                p = new ConfigurablePart(MainGrid, !_showVoxels, PopSeed + attempt, partName, out success);
                attempt++;
            }
            MainGrid.ExistingParts.Add(p);
            _existingParts.Add(p);
        }
    }

    /// <summary>
    /// Populates parts and save the result grid state as an image for a given amount of times
    /// </summary>
    /// <param name="quantity">The quantity of states to save</param>
    protected virtual void PopulateRandomAndSave(int quantity)
    {
        for (int n = 0; n < quantity; n++)
        {
            PopSeed = System.DateTime.Now.Millisecond;
            MainGrid.ClearGrid();
            _existingParts = new List<Part>();

            for (int i = 0; i < _nComponents; i++)
            {
                string partName = $"CP_{i}";
                bool success = false;
                ConfigurablePart p = new ConfigurablePart();
                int attempt = 0;
                while (!success)
                {
                    p = new ConfigurablePart(MainGrid, !_showVoxels, PopSeed + attempt, partName, out success);
                    attempt++;
                }
                //MainGrid.ExistingParts.Add(p);
                _existingParts.Add(p);
            }
            ImageReadWrite.WriteGrid2Image(MainGrid, n);
        }
    }

    /// <summary>
    /// UPDATE THIS TO RUN ON EVIRONMENT Gets the space that the Arrow object represents 
    /// </summary>
    /// <returns>The PPSpace object</returns>
    protected virtual PPSpace GetSpaceFromArrow()
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
                _spaceData = clicked.GetSpaceDebugInfo();
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
    public int CheckResultFromRequest(ReconfigurationRequest request)
    {
        //Return an integer representing the result of the action
        //0 = valid
        //1 = successful
        //2 = destroyed the space
        int result = 0;
        Guid spaceId = request.SpaceId;
        int checkCount = _spaces.Count(s => s.SpaceId == spaceId);
        if (checkCount == 1)
        {
            PPSpace space = MainGrid.GetSpaceById(spaceId);
            if (space != null)
            {
                //Space still exists, evaluate if reconfiguration was successful
                bool success = request.ReconfigurationSuccessful(space);
                request.CurrentIndices = space.Indices.ToArray();
                if (success)
                {
                    result = 1;
                    //print($"{space.Name} reconfiguration was successful. wanted {request.TargetArea}, got {space.VoxelCount}");
                    space.Reconfigure_Area = false;
                    space.Reconfigure_Connectivity = false;
                    _reconfigurationRequests.Remove(request);
                }
                else
                {
                    result = 0;
                    //print($"{space} reconfiguration was not successful. wanted {request.TargetArea}, got {space.VoxelCount}");

                }
            }
        }
        else if (checkCount > 1)
        {
            //print($"{request.SpaceName} was split.");
            //Space was destroyed and split into 2 or more. Differentiate new spaces
            foreach (var space in _spaces.Where(s => s.SpaceId == spaceId))
            {
                space.SpaceId = Guid.NewGuid();
                space.Reconfigure_Area = false;
                space.Reconfigure_Connectivity = false;
            }
            //request.OnSpaceDestruction();
            _reconfigurationRequests.Remove(request);
            result = 2;
        }
        else
        {
            //Space was destroyed, return false
            //print($"{request.SpaceName} was destroyed.");
            //request.OnSpaceDestruction();
            _reconfigurationRequests.Remove(request);
            result = 2;
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

    /// <summary>
    /// Resets the grid after the an Episode is concluded
    /// </summary>
    public void ResetGrid(ReconfigurationRequest request, bool success)
    {
        _completedIndices = request.CurrentIndices;
        _showCompleted = true;
        if (success) _completedColor = Color.green;
        else _completedColor = Color.red;

        StartCoroutine(AnimateCompletionAndRestart());
        //MainGrid.RestartGrid();
        //_spaces = new List<PPSpace>();
        //_boundaries = new List<Voxel>();
        //foreach (ConfigurablePartAgent partAgent in _existingParts.OfType<ConfigurablePart>().Select(p => p.CPAgent))
        //{
        //    partAgent.EndEpisode();
        //}
    }

    protected virtual IEnumerator AnimateCompletionAndRestart()
    {
        yield return new WaitForSeconds(0.5f);
        _showCompleted = false;
        _completedIndices = null;
        MainGrid.RestartGrid();
        _spaces = new List<PPSpace>();
        _boundaries = new List<Voxel>();
        foreach (ConfigurablePartAgent partAgent in _existingParts.OfType<ConfigurablePart>().Select(p => p.CPAgent))
        {
            partAgent.EndEpisode();
        }
    }

    #endregion

    #region Space utilization functions and methods

    /// <summary>
    /// Manually sets a space to be reconfigured
    /// </summary>
    protected virtual void SetSpaceToReconfigure()
    {
        ReconfigurationRequest rr = new ReconfigurationRequest(_selectedSpace, 1, 0);
        _reconfigurationRequests.Add(rr);
        //_selectedSpace.ArtificialReconfigureRequest(0, 1);
    }

    /// <summary>
    /// Sets one of the existing spaces to be reconfigured
    /// </summary>
    protected virtual void SetRandomSpaceToReconfigure() 
    {
        PPSpace space =  new PPSpace();
        bool validRequest = false;
        while (!validRequest)
        {
            UnityEngine.Random.InitState(System.DateTime.Now.Millisecond);
            int i = UnityEngine.Random.Range(0, _spaces.Count);
            space = _spaces[i];

            if (space.BoundaryParts.Count > 0 && !space.IsSpare)
            {
                validRequest = true;
            }
        }
        
        //Set area to be increased
        ReconfigurationRequest rr = new ReconfigurationRequest(space, 1, 0);
        _reconfigurationRequests.Add(rr);
        //space.ArtificialReconfigureRequest(0, 1);
    }

    /// <summary>
    /// IEnumerator to run the daily progression of the occupation simulation
    /// </summary>
    /// <returns></returns>
    protected virtual IEnumerator DailyProgression()
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
    protected virtual void CheckSpaces()
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
    protected virtual void CheckForReconfiguration()
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
    protected virtual void RequestSpace(PPSpaceRequest request)
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
    protected virtual void NextHour()
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
    protected virtual void SetGameObjectsVisibility(bool visible)
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
    protected virtual void DrawState()
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
    protected virtual void DrawBoundaries()
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
    protected virtual void DrawSpaces()
    {
        foreach (var space in MainGrid.Spaces)
        {
            if (!space.IsSpare)
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
    }

    /// <summary>
    /// Draws the space tags
    /// </summary>
    protected virtual void DrawSpaceTags()
    {
        if (_showSpaces)
        {
            var spaces = _spaces.Where(s => !s.IsSpare).ToArray();
            float tagHeight = 3.0f;
            Vector2 tagSize = new Vector2(60, 15);
            foreach (var space in spaces)
            {
                if (!space.IsSpare)
                {
                    string spaceName = space.Name;
                    Vector3 tagWorldPos = transform.position + space.GetCenter() + (Vector3.up * tagHeight);

                    var t = _cam.WorldToScreenPoint(tagWorldPos);
                    Vector2 tagPos = new Vector2(t.x - (tagSize.x / 2), Screen.height - t.y);

                    GUI.Box(new Rect(tagPos, tagSize), spaceName, "spaceTag");
                }
            }
        }
    }

    /// <summary>
    /// Draws a box in the pivot point of the voxel configurable part
    /// </summary>
    protected virtual void DrawPartPivot()
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
    protected virtual void DrawActiveComponent()
    {
        var unfrozenParts = _existingParts.OfType<ConfigurablePart>().Where(p => p.CPAgent.Frozen == false);
        foreach (var up in unfrozenParts)
        {
            var pos = transform.position + (new Vector3(up.Center.x + 0.5f, up.Center.y + 0.5f, up.Center.z + 0.5f) * _voxelSize);
            PP_Drawing.DrawCube(pos + new Vector3(0,4f,0), 0.25f, 0f);
        }
    }

    protected virtual void DrawCompletedSpace()
    {
        foreach (var index in _completedIndices)
        {
            Vector3 realIndex = new Vector3(index.x + 0.5f, index.y + 1.5f, index.z + 0.5f) * _voxelSize;
            PP_Drawing.DrawCube(transform.position + realIndex, MainGrid.VoxelSize, _completedColor);
        }
        
    }

    #endregion
}