using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;

/// <summary>
/// The Agent associated with the configurable part GameObject and the <see cref="ConfigurablePart"/> component
/// </summary>
public class ConfigurablePartAgent : Agent
{
    #region Properties

    private ConfigurablePart _part;
    private PP_Environment _environment;
    public bool Frozen;
    private ReconfigurationRequest _activeRequest;

    #endregion

    #region Basic Methods

    /// <summary>
    /// Returns the <see cref="ConfigurablePart"/> associated with this agent
    /// </summary>
    /// <returns></returns>
    public ConfigurablePart GetPart()
    {
        return _part;
    }

    /// <summary>
    /// Sets the <see cref="ConfigurablePart"/> that should be associated with this agent
    /// and its <see cref="PP_Environment"/>
    /// </summary>
    /// <param name="part">The <see cref="ConfigurablePart"/></param>
    public void SetPart(ConfigurablePart part)
    {
        _part = part;
        _environment = _part._environment;
    }

    /// <summary>
    /// Sets the visibility state of the <see cref="GameObject"/>'s <see cref="MeshRenderer"/>s
    /// </summary>
    /// <param name="visible">The state to be set</param>
    public void SetVisibility(bool visible)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            var mRenderer = transform.GetChild(i).GetComponent<MeshRenderer>();
            mRenderer.enabled = visible;
        }
    }

    /// <summary>
    /// Detroys itself for cleaning up purposes
    /// </summary>
    public void SelfDestroy()
    {
        Destroy(this.gameObject);
    }

    /// <summary>
    /// Assign a request to this agent
    /// </summary>
    /// <param name="request">The <see cref="ReconfigurationRequest"/> to be assigned</param>
    public void SetRequest(ReconfigurationRequest request)
    {
        _activeRequest = request;
    }

    /// <summary>
    /// Clears the active request assigned to this agent
    /// </summary>
    public void ClearRequest()
    {
        _activeRequest = null;
    }

    #endregion

    #region ML Agents Methods

    public override void Initialize()
    {
        //Start with the agent frozen
        FreezeAgent();
    }

    /// <summary>
    /// Method to configure the agent on the beggining of each episode
    /// </summary>
    public override void OnEpisodeBegin()
    {
        //Code for the start of the episode
    }

    /// <summary>
    /// Heuristic method to control the agent manually
    /// </summary>
    /// <param name="actionsOut"></param>
    public override void Heuristic(float[] actionsOut)
    {
        //Code for heuristic mode
        float command = 10f;
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            command = 0f;
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            command = 1f;
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            command = 2f;
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            command = 3f;
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            command = 4f;
        }

        else if (Input.GetKeyDown(KeyCode.E))
        {
            command = 5f;
        }

        else if (Input.GetKeyDown(KeyCode.Q))
        {
            command = 6f;
        }
        actionsOut[0] = command;
    }

    /// <summary>
    /// Method to collect code-based observations
    /// </summary>
    /// <param name="sensor"></param>
    public override void CollectObservations(VectorSensor sensor)
    {
        //Code for observation collection
    }

    /// <summary>
    /// Code for actions, using the discrete action space, 1 branch with size 7
    /// 0 = do nothing
    /// 1 = move +1 in X
    /// 2 = move -1 in X
    /// 3 = move +1 in Z
    /// 4 = move -1 in Z
    /// 5 = rotate clockwise
    /// 6 = rotate counterclockwise
    /// </summary>
    /// <param name="vectorAction"></param>
    public override void OnActionReceived(float[] vectorAction)
    {
        //Parse the vectorAction to int
        int movement = Mathf.RoundToInt(vectorAction[0]);

        //Store the existing spaces
        List<PPSpace> existingSpaces = _environment.GetCurrentSpaces();

         if (movement == 0)
        {
            FreezeAgent();
            _activeRequest.UnfreezeRandomAgent();

        }

        if (movement == 1)
        {
            //Tries to move +1 in X
            if (_part.MoveInX(1))
            {
                //Check if action didn't destroy the space
                if (_environment.CheckResultFromRequest(_activeRequest))
                {
                    //Action was valid, apply to gameobject
                    transform.position += new Vector3(1, 0, 0) * _part.Grid.VoxelSize;
                }
                //Space was destroyed, undo action
                else
                {
                    //print("Action destroyed space, undid action.");
                    //_part.MoveInX(-1);
                    //_environment.ForceResetSpaces(existingSpaces);
                    //_activeRequest.UnfreezeRandomAgent();
                    //apply penalty
                }
            }
            else
            {
                //tried an illegal movement, apply penalty
            }
        }
        
        else if (movement == 2)
        {
            //Tries to move -1 in X
            if (_part.MoveInX(-1))
            {
                //Check if action didn't destroy the space
                if (_environment.CheckResultFromRequest(_activeRequest))
                {
                    //Action was valid, apply to gameobject
                    transform.position += new Vector3(-1, 0, 0) * _part.Grid.VoxelSize;
                }
                //Space was destroyed, undo action
                else
                {
                    //print("Action destroyed space, undid action.");
                    //_part.MoveInX(1);
                    //_environment.ForceResetSpaces(existingSpaces);
                    //_activeRequest.UnfreezeRandomAgent();
                    //apply penalty
                }
            }
            else
            {
                //tried an illegal movement, apply penalty
            }
        }
        
        else if (movement == 3)
        {
            //Tries to move +1 in Z
            if (_part.MoveInZ(1))
            {
                //Check if action didn't destroy the space
                if (_environment.CheckResultFromRequest(_activeRequest))
                {
                    //Action was valid, apply to gameobject
                    transform.position += new Vector3(0, 0, 1) * _part.Grid.VoxelSize;
                }
                //Space was destroyed, undo action
                else
                {
                    //print("Action destroyed space, undid action.");
                    //_part.MoveInZ(-1);
                    //_environment.ForceResetSpaces(existingSpaces);
                    //_activeRequest.UnfreezeRandomAgent();
                    //apply penalty
                }
            }
            else
            {
                //tried an illegal movement, apply penalty
            }
        }
        
        else if (movement == 4)
        {
            //Tries to move -1 in Z
            if (_part.MoveInZ(-1))
            {
                //Check if action didn't destroy the space
                if (_environment.CheckResultFromRequest(_activeRequest))
                {
                    //Action was valid, apply to gameobject
                    transform.position += new Vector3(0, 0, -1) * _part.Grid.VoxelSize;
                }
                //Space was destroyed, undo action
                else
                {
                    //print("Action destroyed space, undid action.");
                    //_part.MoveInZ(1);
                    //_environment.ForceResetSpaces(existingSpaces);
                    //_activeRequest.UnfreezeRandomAgent();
                    //apply penalty
                }
            }
            else
            {
                //tried an illegal movement, apply penalty
            }
        }
        
        else if (movement == 5)
        {
            //Tries to rotate component clockwise
            if (_part.RotateComponent(1))
            {
                //Check if action didn't destroy the space
                if (_environment.CheckResultFromRequest(_activeRequest))
                {
                    //Action was valid, apply to gameobject
                    var currentRotation = transform.rotation;
                    transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y + 90f, currentRotation.eulerAngles.z);
                }
                //Space was destroyed, undo action
                else
                {
                    //print("Action destroyed space, undid action.");
                    //_part.RotateComponent(-1);
                    //_environment.ForceResetSpaces(existingSpaces);
                    //_activeRequest.UnfreezeRandomAgent();
                    //apply penalty
                }
            }
            else
            {
                //tried an illegal movement, apply penalty
            }
        }
        
        else if (movement == 6)
        {
            if (_part.RotateComponent(-1))
            {
                //Check if action didn't destroy the space
                if (_environment.CheckResultFromRequest(_activeRequest))
                {
                    //Action was valid, apply to gameobject
                    var currentRotation = transform.rotation;
                    transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y - 90f, currentRotation.eulerAngles.z);
                }
                //Space was destroyed, undo action
                else
                {
                    //print("Action destroyed space, undid action.");
                    //_part.RotateComponent(-1);
                    //_environment.ForceResetSpaces(existingSpaces);
                    //_activeRequest.UnfreezeRandomAgent();
                    //apply penalty
                }
            }
            //Space was destroyed, undo action
            else
            {
                //tried an illegal movement, apply penalty
            }
        }
    }

    /// <summary>
    /// Freezes the agent to prevent actions
    /// </summary>
    public void FreezeAgent()
    {
        Frozen = true;
    }

    public void UnfreezeAgent()
    {
        Frozen = false;
    }

    public void SetAsComplete(bool success)
    {
        if (success)
        {
            //apply reward
        }
        else
        {
            //apply destruction penalty
        }
        Frozen = true;
        _activeRequest = null;
    }

    #endregion

    #region Unity Methods

    private void Update()
    {
        ////THIS GUYS SHOULD BE MOVED TO HEURISTICS
        //if (Input.GetKeyDown(KeyCode.LeftArrow))
        //{
        //    if (_part.MoveInX(-1))
        //    {
        //        transform.position += new Vector3(-1, 0, 0) * _part.Grid.VoxelSize;
        //    }
        //}
        //if (Input.GetKeyDown(KeyCode.RightArrow))
        //{
        //    if (_part.MoveInX(1))
        //    {
        //        transform.position += new Vector3(1, 0, 0) * _part.Grid.VoxelSize;
        //    }
        //}
        //if (Input.GetKeyDown(KeyCode.UpArrow))
        //{
        //    if (_part.MoveInZ(1))
        //    {
        //        transform.position += new Vector3(0, 0, 1) * _part.Grid.VoxelSize;
        //    }
        //}
        //if (Input.GetKeyDown(KeyCode.DownArrow))
        //{
        //    if (_part.MoveInZ(-1))
        //    {
        //        transform.position += new Vector3(0, 0, -1) * _part.Grid.VoxelSize;
        //    }
        //}

        //if (Input.GetKeyDown(KeyCode.E))
        //{
        //    if (_part.RotateComponent(1))
        //    {
        //        var currentRotation = transform.rotation;
        //        transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y + 90f, currentRotation.eulerAngles.z);
        //    }
        //}

        //if (Input.GetKeyDown(KeyCode.Q))
        //{
        //    if (_part.RotateComponent(-1))
        //    {
        //        var currentRotation = transform.rotation;
        //        transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y - 90f, currentRotation.eulerAngles.z);
        //    }
        //}
    }

    private void FixedUpdate()
    {
        if (!Frozen)
        {
            RequestDecision();
        }
    }

    #endregion
}
