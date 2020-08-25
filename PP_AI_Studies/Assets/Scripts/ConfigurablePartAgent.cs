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
    #region Unity properties

    public Animator Anim { get; private set; }

    private Vector3 _initPosition;
    private Vector3 _endPosition;

    private Quaternion _initRotationQ;
    private Quaternion _endRotationQ;

    public bool IsMoving = false;
    public bool IsRotating = false;

    #endregion

    #region Agent properties

    private ConfigurablePart _part;
    private PP_Environment _environment;
    public bool Frozen;
    private ReconfigurationRequest _activeRequest = new ReconfigurationRequest();
    private int _stepsTaken = 0;
    private int _stepsCap = 10;
    public bool StepsEnded = false;
    
    //Rewards and penalties
    private float _destroyedPenalty = -1f;
    private float _existentialPenalty = -0.1f;
    private float _invalidMovementPenalty = -0.1f;
    private float _validReward = 0.1f;
    private float _successReward = 1f;

    private bool _training = true;
    private bool _manualAnimation = false;

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
        FreezeAgent();
        _part.ResetPosition();
        ClearRequest();
        _stepsTaken = 0;
        StepsEnded = false;
        if (_training) _environment.InitializedAgents++;
    }

    /// <summary>
    /// Heuristic method to control the agent manually
    /// </summary>
    /// <param name="actionsOut"></param>
    public override void Heuristic(float[] actionsOut)
    {
        if (!_manualAnimation)
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
        if (!_manualAnimation)
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

            //Add a representation of the current state
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
        if (!_manualAnimation)
        {
            _stepsTaken++;
            //Parse the vectorAction to int
            int movement = Mathf.RoundToInt(vectorAction[0]);

            if (movement == 0)
            {
                //FreezeAgent();
                //_activeRequest.UnfreezeRandomAgent();

                //Idle
                return;
            }

            else if (movement == 1)
            {
                //Tries to move +1 in X
                if (_part.MoveInX(1, false, false))
                {
                    //Action was valid, apply to gameobject
                    transform.position += new Vector3(1, 0, 0) * _part.Grid.VoxelSize;
                    AddReward(_validReward);
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
                if (_part.MoveInX(-1, false, false))
                {
                    //Action was valid, apply to gameobject
                    transform.position += new Vector3(-1, 0, 0) * _part.Grid.VoxelSize;
                    AddReward(_validReward);
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
                if (_part.MoveInZ(1, false, false))
                {
                    //Action was valid, apply to gameobject
                    transform.position += new Vector3(0, 0, 1) * _part.Grid.VoxelSize;
                    AddReward(_validReward);
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
                if (_part.MoveInZ(-1, false, false))
                {
                    //Action was valid, apply to gameobject
                    transform.position += new Vector3(0, 0, -1) * _part.Grid.VoxelSize;
                    AddReward(_validReward);
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
                if (_part.RotateComponent(1, false, false))
                {
                    //Action was valid, apply to gameobject
                    var currentRotation = transform.rotation;
                    transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y + 90f, currentRotation.eulerAngles.z);
                    AddReward(_validReward);
                }
                else
                {
                    //tried an illegal movement, apply penalty
                    AddReward(_invalidMovementPenalty);
                }
            }

            else if (movement == 6)
            {
                if (_part.RotateComponent(-1, false, false))
                {
                    //Action was valid, apply to gameobject
                    var currentRotation = transform.rotation;
                    transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y - 90f, currentRotation.eulerAngles.z);
                    AddReward(_validReward);
                }
                else
                {
                    //tried an illegal movement, apply penalty
                    AddReward(_invalidMovementPenalty);
                }
            }

            //Freeze the agent once it reaches the step cap
            if (_stepsTaken >= _stepsCap)
            {
                StepsEnded = true;
                FreezeAgent();
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
     
    /// <summary>
    /// Unfreezes this agent 
    /// </summary>
    public void UnfreezeAgent()
    {
        Frozen = false;
    }

    /// <summary>
    /// Triggers the analysis of the grid and evaluates the result
    /// </summary>
    private void VerifyResult()
    {
        //_environment.AnalyzeGridUpdateSpaces();
        //Check the result of the actions
        int actionResult = _environment.CheckResultFromRequest(_activeRequest);
        if (actionResult != 2)
        {
            //Action was valid, apply to gameobject
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
    }

    /// <summary>
    /// Controls for the movement of the components for manual animation scenarion
    /// regular WASD
    /// </summary>
    private void ManualAnimationMovement()
    {
        if (_manualAnimation && !Frozen)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                if (_part.MoveInX(-1, false, false))
                {
                    transform.position += new Vector3(-1, 0, 0) * _part.Grid.VoxelSize;
                }
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                if (_part.MoveInX(1, false, false))
                {
                    transform.position += new Vector3(1, 0, 0) * _part.Grid.VoxelSize;
                }
            }
            else if (Input.GetKeyDown(KeyCode.W))
            {
                if (_part.MoveInZ(1, false, false))
                {
                    transform.position += new Vector3(0, 0, 1) * _part.Grid.VoxelSize;
                }
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                if (_part.MoveInZ(-1, false, false))
                {
                    transform.position += new Vector3(0, 0, -1) * _part.Grid.VoxelSize;
                }
            }

            else if (Input.GetKeyDown(KeyCode.E))
            {
                if (_part.RotateComponent(1, false, false))
                {
                    var currentRotation = transform.rotation;
                    transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y + 90f, currentRotation.eulerAngles.z);
                }
            }

            else if (Input.GetKeyDown(KeyCode.Q))
            {
                if (_part.RotateComponent(-1, false, false))
                {
                    var currentRotation = transform.rotation;
                    transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y - 90f, currentRotation.eulerAngles.z);
                }
            }

            else if (Input.GetKeyDown(KeyCode.T))
            {
                Anim.speed = 5f;
                Anim.SetBool("isMoving", true);
            }
        }
    }

    #endregion

    #region Unity Methods

    private void Awake()
    {
        FreezeAgent();
        Anim = GetComponent<Animator>();
    }

    private void Update()
    {
        ManualAnimationMovement();
    }

    private void FixedUpdate()
    {
        if (!Frozen && !_manualAnimation)
        {
            RequestDecision();
        }
    }

    #endregion

    #region Animation Methods

    /// <summary>
    /// Stores the current position and rotation as the initial state
    /// </summary>
    public void StoreInitialState()
    {
        _initPosition = transform.position;
        _initRotationQ = transform.localRotation;
    }

    /// <summary>
    /// Stores the current position and rotation as the target state of the animation
    /// </summary>
    public void PrepareAnimation()
    {
        _endPosition = transform.position;
        _endRotationQ = transform.localRotation;
        
        transform.position = _initPosition;
        transform.localRotation = _initRotationQ;
    }

    /// <summary>
    /// Triggers the start of the animation
    /// </summary>
    /// <returns>The bool that determines if the animation is necessary</returns>
    public bool TriggerAnimation()
    {
        //Check if position is different than the initial one
        if (_initPosition != _endPosition)
        {
            IsMoving = true;
        }
        //Check if rotation is different than the initial one
        if (_initRotationQ != _endRotationQ)
        {
            IsRotating = true;
        }

        //Analyze if any animation is required
        if (IsMoving || IsRotating)
        {
            Anim.SetBool("isMoving", true);
            return true;
        }
        else return false;
    }

    /// <summary>
    /// Finilizes the animation
    /// </summary>
    public void EndAnimation()
    {
        Anim.SetBool("isMoving", false);
        IsMoving = false;
        IsRotating = false;
    }

    /// <summary>
    /// Returns the target position of the animation
    /// </summary>
    /// <returns><see cref="_endPosition"/> of the reconfiguration of this agent</returns>
    public Vector3 GetTargetPosition()
    {
        return _endPosition;
    }

    /// <summary>
    /// Returns the target rotation of the animation
    /// </summary>
    /// <returns>the <see cref="_endRotationQ"/> of the reconfiguration of this agent as a quaternion</returns>
    public Quaternion GetTargetRotationQ()
    {
        return _endRotationQ;
    }

    /// <summary>
    /// Modifies the speed that the animation should be played in
    /// </summary>
    /// <param name="speed">The new speed</param>
    public void SetAnimatorSpeed(float speed)
    {
        Anim.speed = speed;
    }

    #endregion
}