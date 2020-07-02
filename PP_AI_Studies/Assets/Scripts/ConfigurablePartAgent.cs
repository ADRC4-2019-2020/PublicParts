﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;

/// <summary>
/// The Agent associated with the configurable part GameObject and the <see cref="ConfigurablePart"/> component
/// </summary>
public class ConfigurablePartAgent : Agent
{
    #region Unity properties

    public Animator Anim { get; private set; }

    private Vector3 _initPosition;
    private Vector3 _endPosition;
    private Vector3 _initRotation;
    private Vector3 _endRotation;

    public bool IsMoving = false;

    #endregion

    #region Properties

    private ConfigurablePart _part;
    private PP_Environment _environment;
    public bool Frozen;
    private ReconfigurationRequest _activeRequest = new ReconfigurationRequest();
    
    //Rewards and penalties
    private float _destroyedPenalty = -1f;
    private float _existentialPenalty = -0.1f;
    private float _invalidMovementPenalty = -0.1f;
    private float _validReward = 0.1f;
    private float _successReward = 1f;

    private bool _training = true;
    private bool _manual = true;

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
            var child = transform.GetChild(i);
            if (child.GetComponent<MeshRenderer>() != null)
            {
                var mRenderer = child.GetComponent<MeshRenderer>();
                mRenderer.enabled = visible;
            }
            
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
        _activeRequest = new ReconfigurationRequest();
    }

    #endregion

    #region ML Agents Methods

    /// <summary>
    /// Method to configure the agent on the beggining of each episode
    /// </summary>
    public override void OnEpisodeBegin()
    {
        //Code for the start of the episode
        FreezeAgent();
        ClearRequest();
        //_part.FindNewPosition(_environment.PopSeed);
        if (_training) _environment.InitializedAgents++;
        //print($"Initialized agent of {_part.Name}");
    }

    /// <summary>
    /// Heuristic method to control the agent manually
    /// </summary>
    /// <param name="actionsOut"></param>
    public override void Heuristic(float[] actionsOut)
    {
        if (!_manual)
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
    }

    /// <summary>
    /// Method to collect code-based observations
    /// </summary>
    /// <param name="sensor"></param>
    public override void CollectObservations(VectorSensor sensor)
    {
        if (!_manual)
        {
            //Code for observation collection [9 OBSERVATIONS TOTAL]

            //Current orientation of the part Horizontal = 0, vertical  = 1. [1 OBSERVATION]
            if (_part.Orientation == PartOrientation.Horizontal) sensor.AddObservation(0f);
            else sensor.AddObservation(1f);

            //The current rotation of the part [1 OBSERVATION]
            sensor.AddObservation(_part.Rotation);

            //the reference index of the part (X and Z only) [2 OBSERVATIONS]
            sensor.AddObservation(_part.ReferenceIndex.x);
            sensor.AddObservation(_part.ReferenceIndex.z);

            //The active request properties [2 OBSERVATIONS]
            sensor.AddObservation(_activeRequest.TargetArea);
            sensor.AddObservation(_activeRequest.TargetConnections);

            //The properties of the grid [3 OBSERVATIONS]
            //Size
            sensor.AddObservation(_part.Grid.Size.x);
            sensor.AddObservation(_part.Grid.Size.y);
            //Amount of spaces
            sensor.AddObservation(_part.Grid.Spaces.Count);
        }
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
        if (!_manual)
        {
            //Parse the vectorAction to int
            int movement = Mathf.RoundToInt(vectorAction[0]);

            //Store the existing spaces to apply
            //List<PPSpace> existingSpaces = _environment.GetCurrentSpaces();

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
                    int actionResult = _environment.CheckResultFromRequest(_activeRequest);
                    if (actionResult != 2)
                    {
                        //Action was valid, apply to gameobject
                        transform.position += new Vector3(1, 0, 0) * _part.Grid.VoxelSize;
                        if (actionResult == 1)
                        {
                            //Action acheived desired reconfiguration
                            TriggerEndEpisode(_successReward);
                        }
                        else
                        {
                            AddReward(_validReward);
                        }
                    }
                    //Space was destroyed, undo action
                    else
                    {
                        //print("Action destroyed space, undid action.");
                        TriggerEndEpisode(_destroyedPenalty);
                        //apply penalty
                    }
                }
                else
                {
                    //tried an illegal movement, apply penalty
                    AddReward(_invalidMovementPenalty);
                }
            }

            else if (movement == 2)
            {
                //Tries to move -1 in X
                if (_part.MoveInX(-1))
                {
                    //Check if action didn't destroy the space
                    int actionResult = _environment.CheckResultFromRequest(_activeRequest);
                    if (actionResult != 2)
                    {
                        //Action was valid, apply to gameobject
                        transform.position += new Vector3(-1, 0, 0) * _part.Grid.VoxelSize;
                        if (actionResult == 1)
                        {
                            //Action acheived desired reconfiguration
                            TriggerEndEpisode(_successReward);
                        }
                        else
                        {
                            AddReward(_validReward);
                        }
                    }
                    //Space was destroyed, undo action
                    else
                    {
                        //print("Action destroyed space, undid action.");
                        TriggerEndEpisode(_destroyedPenalty);
                    }
                }
                else
                {
                    //tried an illegal movement, apply penalty
                    AddReward(_invalidMovementPenalty);
                }

            }

            else if (movement == 3)
            {
                //Tries to move +1 in Z
                if (_part.MoveInZ(1))
                {
                    //Check if action didn't destroy the space
                    int actionResult = _environment.CheckResultFromRequest(_activeRequest);
                    if (actionResult != 2)
                    {
                        //Action was valid, apply to gameobject
                        transform.position += new Vector3(0, 0, 1) * _part.Grid.VoxelSize;
                        if (actionResult == 1)
                        {
                            //Action acheived desired reconfiguration
                            TriggerEndEpisode(_successReward);
                        }
                        else
                        {
                            AddReward(_validReward);
                        }
                    }
                    //Space was destroyed, undo action
                    else
                    {
                        //print("Action destroyed space, undid action.");
                        TriggerEndEpisode(_destroyedPenalty);
                    }
                }
                else
                {
                    //tried an illegal movement, apply penalty
                    AddReward(_invalidMovementPenalty);
                }
            }

            else if (movement == 4)
            {
                //Tries to move -1 in Z
                if (_part.MoveInZ(-1))
                {
                    //Check if action didn't destroy the space
                    int actionResult = _environment.CheckResultFromRequest(_activeRequest);
                    if (actionResult != 2)
                    {
                        //Action was valid, apply to gameobject
                        transform.position += new Vector3(0, 0, -1) * _part.Grid.VoxelSize;
                        if (actionResult == 1)
                        {
                            //Action acheived desired reconfiguration
                            TriggerEndEpisode(_successReward);
                        }
                        else
                        {
                            AddReward(_validReward);
                        }
                    }
                    //Space was destroyed, undo action
                    else
                    {
                        //print("Action destroyed space, undid action.");
                        TriggerEndEpisode(_destroyedPenalty);
                    }
                }
                else
                {
                    //tried an illegal movement, apply penalty
                    AddReward(_invalidMovementPenalty);
                }
            }

            else if (movement == 5)
            {
                //Tries to rotate component clockwise
                if (_part.RotateComponent(1))
                {
                    //Check if action didn't destroy the space
                    int actionResult = _environment.CheckResultFromRequest(_activeRequest);
                    if (actionResult != 2)
                    {
                        //Action was valid, apply to gameobject
                        var currentRotation = transform.rotation;
                        transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y + 90f, currentRotation.eulerAngles.z);
                        if (actionResult == 1)
                        {
                            //Action acheived desired reconfiguration
                            TriggerEndEpisode(_successReward);
                        }
                        else
                        {
                            AddReward(_validReward);
                        }
                    }
                    //Space was destroyed, undo action
                    else
                    {
                        //print("Action destroyed space, undid action.");
                        TriggerEndEpisode(_destroyedPenalty);
                    }
                }
                else
                {
                    //tried an illegal movement, apply penalty
                    AddReward(_invalidMovementPenalty);
                }
            }

            else if (movement == 6)
            {
                if (_part.RotateComponent(-1))
                {
                    //Check if action didn't destroy the space
                    int actionResult = _environment.CheckResultFromRequest(_activeRequest);
                    if (actionResult != 2)
                    {
                        //Action was valid, apply to gameobject
                        var currentRotation = transform.rotation;
                        transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y - 90f, currentRotation.eulerAngles.z);
                        if (actionResult == 1)
                        {
                            //Action acheived desired reconfiguration
                            TriggerEndEpisode(_successReward);
                        }
                        else
                        {
                            AddReward(_validReward);
                        }
                    }
                    //Space was destroyed, undo action
                    else
                    {
                        //print("Action destroyed space, undid action.");
                        TriggerEndEpisode(_destroyedPenalty);
                    }
                }
                else
                {
                    //tried an illegal movement, apply penalty
                    AddReward(_invalidMovementPenalty);
                }
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

    /// <summary>
    /// Triggers the end of an episode due to failure or success
    /// </summary>
    /// <param name="reward">The reward or penalty to be applied to the agent</param>
    private void TriggerEndEpisode(float reward)
    {
        AddReward(reward);
        bool success = false;
        if (reward > 0) success = true;
        _environment.ResetGrid(_activeRequest, success);
        //EndEpisode();
    }

    #endregion

    #region Unity Methods

    private void Awake()
    {
        Anim = GetComponent<Animator>();
    }

    /// <summary>
    /// Method utilized to move the components in manual mode
    /// </summary>
    private void Update()
    {
        if (_manual && !Frozen)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                if (_part.MoveInX(-1, false, false))
                {
                    transform.position += new Vector3(-1, 0, 0) * _part.Grid.VoxelSize;
                }
            }
            if (Input.GetKeyDown(KeyCode.D))
            {
                if (_part.MoveInX(1, false, false))
                {
                    transform.position += new Vector3(1, 0, 0) * _part.Grid.VoxelSize;
                }
            }
            if (Input.GetKeyDown(KeyCode.W))
            {
                if (_part.MoveInZ(1, false, false))
                {
                    transform.position += new Vector3(0, 0, 1) * _part.Grid.VoxelSize;
                }
            }
            if (Input.GetKeyDown(KeyCode.S))
            {
                if (_part.MoveInZ(-1, false, false))
                {
                    transform.position += new Vector3(0, 0, -1) * _part.Grid.VoxelSize;
                }
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (_part.RotateComponent(1, false, false))
                {
                    var currentRotation = transform.rotation;
                    transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y + 90f, currentRotation.eulerAngles.z);
                }
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                if (_part.RotateComponent(-1, false, false))
                {
                    var currentRotation = transform.rotation;
                    transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y - 90f, currentRotation.eulerAngles.z);
                }
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                Anim.speed = 5f;
                Anim.SetBool("isMoving", true);
            }
        }
        
    }

    private void FixedUpdate()
    {
        if (!Frozen && !_manual)
        {
            RequestDecision();
        }
    }

    /// <summary>
    /// Tells the agent to store its current position
    /// </summary>
    public void StoreInitialPosition()
    {
        _initPosition = transform.position;
        _initRotation = transform.localEulerAngles;
    }

    public void PrepareAnimation()
    {
        _endPosition = transform.position;
        _endRotation = transform.localEulerAngles;
        if (_initPosition != _endPosition || _initRotation != _endRotation)
        {
            transform.position = _initPosition;
            transform.localEulerAngles = _initRotation;
            Anim.SetBool("isMoving", true);
            IsMoving = true;
        }
    }

    private bool _moveDone = false;
    private bool _rotationDone = false;

    public void AnimateTransition(float currentSpeed)
    {
        bool animationState = Anim.GetCurrentAnimatorStateInfo(0).length >= Anim.GetCurrentAnimatorStateInfo(0).normalizedTime;
        if (!_moveDone)
        {
            if (!animationState)
            {
                transform.position = Vector3.MoveTowards(transform.position, _endPosition, 0.01f);

                if (Vector3.Distance(transform.position, _endPosition) < 0.0001f)
                {
                    transform.position = _endPosition;
                    _moveDone = true;
                }
                return;
            }
        }
        else
        {
            Anim.SetBool("isMoving", false);
        }

        // THIS SHOULD BE ANOTHER IENUMERATOR, IT DOESEN'T WORK OTHERWISE

        if (!animationState)
        {

        }
        








        //if (!animationState)
        //{
        //    if (transform.position != _endPosition)
        //    {
        //        transform.position = Vector3.MoveTowards(transform.position, _endPosition, 0.01f);

        //        if (Vector3.Distance(transform.position, _endPosition) < 0.0001f)
        //        {
        //            transform.position = _endPosition;
        //        }
        //    }
        //    else
        //    {
        //        Anim.SetBool("isMoving", false);
        //    }
        //}
        //else
        //{
        //    IsMoving = false;
        //}
    }

    #endregion
}
