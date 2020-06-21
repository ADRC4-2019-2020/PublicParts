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
    private bool _frozen;

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
    /// </summary>
    /// <param name="part">The <see cref="ConfigurablePart"/></param>
    public void SetPart(ConfigurablePart part)
    {
        _part = part;
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
        float command = 0f;
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
        //Do nothing if the agent is frozen
        //if (_frozen) return;

        //Parse the vectorAction to int
        int movement = Mathf.RoundToInt(vectorAction[0]);
        
        // if (movement == 0) do nothing
        
        if (movement == 1)
        {
            //Tries to move +1 in X
            if (_part.MoveInX(1))
            {
                transform.position += new Vector3(1, 0, 0) * _part.Grid.VoxelSize;
            }
            else
            {
                //apply penalty
            }
        }
        
        else if (movement == 2)
        {
            //Tries to move -1 in X
            if (_part.MoveInX(-1))
            {
                transform.position += new Vector3(-1, 0, 0) * _part.Grid.VoxelSize;
            }
            else
            {
                //apply penalty
            }
        }
        
        else if (movement == 3)
        {
            //Tries to move +1 in Z
            if (_part.MoveInZ(1))
            {
                transform.position += new Vector3(0, 0, 1) * _part.Grid.VoxelSize;
            }
            else
            {
                //apply penalty
            }
        }
        
        else if (movement == 4)
        {
            //Tries to move -1 in Z
            if (_part.MoveInZ(-1))
            {
                transform.position += new Vector3(0, 0, -1) * _part.Grid.VoxelSize;
            }
            else
            {
                //apply penalty
            }
        }
        
        else if (movement == 5)
        {
            //Tries to rotate component clockwise
            if (_part.RotateComponent(1))
            {
                var currentRotation = transform.rotation;
                transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y + 90f, currentRotation.eulerAngles.z);
            }
            else
            {
                //apply penalty
            }
        }
        
        else if (movement == 6)
        {
            if (_part.RotateComponent(-1))
            {
                var currentRotation = transform.rotation;
                transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y - 90f, currentRotation.eulerAngles.z);
            }
            else
            {
                //apply penalty
            }
        }
    }

    /// <summary>
    /// Freezes the agent to prevent actions
    /// </summary>
    public void FreezeAgent()
    {
        _frozen = true;
    }

    public void UnfreezeAgent()
    {
        _frozen = false;
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
        if (!_frozen)
        {
            RequestDecision();
        }
    }

    #endregion
}
