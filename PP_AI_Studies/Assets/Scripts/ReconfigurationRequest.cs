using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles a reconfiguration request and its results
/// </summary>
public class ReconfigurationRequest
{
    #region Properties and fields

    //The GUID of the space that needs to be reconfigured
    public Guid SpaceId { get; private set; }
    
    //The name of the space that needs to be reconfigured
    public string SpaceName { get; private set; }
    
    //The current indices occupied by the space
    public Vector3Int[] CurrentIndices;
    
    //The target parameters for the space
    public int TargetArea { get; private set; } = 0;
    public int TargetConnections { get; private set; } = 0;
    
    //The evaluation parameters, to add or remove accordingly
    private int _areaModifier = 8;
    private int _connectivityModifier = 4;
    
    //The components to be reconfigured
    private ConfigurablePartAgent[] _agents2Reconfigure;

    private int _currentAgent = 0;

    //The centre of the target space
    private Vector3Int _spaceCenter;

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor for a ReconfigurationRequest for a given space, with instructions for 
    /// Area and Connectivity reconfiguration.
    /// [+1] = Increase
    /// [-1] = Decrese
    /// [0]  = Don't change
    /// </summary>
    /// <param name="space">The space that should be reconfigured</param>
    /// <param name="areaDirection">The direction for the area reconfiguration</param>
    /// <param name="connectivityDirection"></param>
    public ReconfigurationRequest(PPSpace space, int areaDirection, int connectivityDirection)
    {
        space.ValidateSpace();
        SpaceId = space.SpaceId;
        SpaceName = space.Name;
        _spaceCenter = new Vector3Int((int)space.GetCenter().x, 0, (int)space.GetCenter().z);
        CurrentIndices = space.Indices.ToArray();
        //Area defined by the Voxel count
        int currentArea = space.VoxelCount;
        //Conectivity defined by the number of connections [voxels]
        int currentConnectivity = space.NumberOfConnections;

        space.ArtificialReconfigureRequest(areaDirection, connectivityDirection);

        //Set the target area
        if (areaDirection == 1) TargetArea = currentArea + _areaModifier;
        else if (areaDirection == -1) TargetArea = currentArea - _areaModifier;
        else if (areaDirection == 0) TargetArea = 0;

        //Set the target connections
        if (connectivityDirection == 1) TargetConnections = currentConnectivity + _connectivityModifier;
        else if (connectivityDirection == -1) TargetConnections = currentConnectivity - _connectivityModifier;
        else if (connectivityDirection == 0) TargetConnections = 0;

        _agents2Reconfigure = space.BoundaryParts.Select(p => p.CPAgent).ToArray();
        foreach (var part in _agents2Reconfigure)
        {
            part.ClearRequest();
            part.SetRequest(this);
        }
        //UnfreezeRandomAgent();
        UnfreezeAgents();
    }

    /// <summary>
    /// An empty constructor for a <see cref="ReconfigurationRequest"/>
    /// </summary>
    public ReconfigurationRequest()
    {
        SpaceId = new Guid();
        SpaceName = "";
        TargetArea = 0;
        TargetConnections = 0;
    }

    #endregion

    #region Reconfiguration evaluation

    /// <summary>
    /// Checks if the reconfiguration has been successful
    /// </summary>
    /// <param name="space">The space to be checked</param>
    /// <returns>The result if the reconfiguration was successful</returns>
    public bool ReconfigurationSuccessful(PPSpace space)
    {
        //Get the current parameters [area = Voxel count; connectivity = number of connections]
        int currentArea = space.VoxelCount;
        int currentConnectivity = space.NumberOfConnections;

        //Create the result validators, starting with true
        bool areaSuccessful = true;
        bool connectivitySuccessful = true;
        
        //Check if reconfiguration was successful, within a margin of +- 50% of the target parameter
        //Check if area objective has been reached [0 = no area reconfiguration requested]
        if (TargetArea != 0 )
        {
            if (currentArea < TargetArea - (_areaModifier / 2) || currentArea > TargetArea + (_areaModifier / 2))
            {
                areaSuccessful = false;
            }
        }

        //Check if connectivity objective has been reached [0 = no connectivity reconfiguration requested]
        if (TargetConnections != 0)
        {
            if (currentConnectivity < TargetConnections - (_connectivityModifier / 2) || currentConnectivity > TargetConnections + (_connectivityModifier / 2))
            {
                connectivitySuccessful = false;
            }
        }

        //Check if the request has been fullfiled
        if (areaSuccessful && connectivitySuccessful)
        {
            //OnReconfigurationSuccessful();
            return true;
        }
        else
        {
            //UnfreezeRandomAgent();
            return false;
        }
    }

    /// <summary>
    /// Checks if the all the agents have finilized their steps
    /// </summary>
    /// <returns>The bool representing it</returns>
    public bool AllAgentsFinished()
    {
        return _agents2Reconfigure.All(a => a.StepsEnded);
    }

    #endregion

    #region Exterior objects methods

    /// <summary>
    /// Allows to call the next agent to take an action externally
    /// </summary>
    public void RequestNextAction()
    {
        if (_agents2Reconfigure.Length >= 0)
        {
            if (_currentAgent >= _agents2Reconfigure.Length)
            {
                _currentAgent = 0;
            }

            _agents2Reconfigure[_currentAgent++].RequestDecision();
            //_currentAgent++;
        }
    }

    /// <summary>
    /// Apply the same reward to the agents associated with this request
    /// </summary>
    /// <param name="val">The reward value</param>
    public void ApplyReward(float val)
    {
        foreach (var agent in _agents2Reconfigure)
        {
            agent.AddReward(val);
        }
    }

    /// <summary>
    /// Sets the <see cref="ConfigurablePartAgent"/>s of all the parts to be reconfigured ans Unfrozen
    /// </summary>
    private void UnfreezeAgents()
    {
        foreach (var part in _agents2Reconfigure)
        {
            part.UnfreezeAgent();
        }
    }

    /// <summary>
    /// Sets the <see cref="ConfigurablePartAgent"/> of all parts that should be reconfigured as Frozen
    /// </summary>
    private void FreezeAgents()
    {
        foreach (var part in _agents2Reconfigure)
        {
            //Freeze the agents so they stop making decisions
            part.FreezeAgent();
        }
    }

    /// <summary>
    /// Unfreezes a random agent. This is used to allow only one agent to act per turn
    /// </summary>
    public void UnfreezeRandomAgent()
    {
        int i = UnityEngine.Random.Range(0, _agents2Reconfigure.Length);
        _agents2Reconfigure[i].UnfreezeAgent();
    }

    /// <summary>
    /// Method to be called if the target space does not exist anymore
    /// </summary>
    public void OnSpaceDestruction()
    {
        FreezeAgents();
        //foreach (var agnt in _agents2Reconfigure)
        //{
        //    agnt.SetAsComplete(false);
        //}
    }

    /// <summary>
    /// Method to be called if the reconfiguration for this request has been successful
    /// </summary>
    public void OnReconfigurationSuccessful()
    {
        FreezeAgents();
        //foreach (var agnt in _agents2Reconfigure)
        //{
        //    agnt.SetAsComplete(true);
        //}
    }

    /// <summary>
    /// Get the <see cref="Vector3Int"/> of the space's average center
    /// </summary>
    /// <returns>The center as a <see cref="Vector3Int"/></returns>
    public Vector3Int GetTargetSpaceCenter()
    {
        return _spaceCenter;
    }

    /// <summary>
    /// Get the names of the parts associated with the agents associaciated with this request
    /// </summary>
    /// <returns>A single string with all names</returns>
    public string GetAgentsNames()
    {
        string names = "";
        foreach (var agent in _agents2Reconfigure)
        {
            names += ", " + agent.GetPart().Name;
        }
        return names;
    }

    public void ReleaseAgents()
    {
        foreach (var agent in _agents2Reconfigure)
        {
            agent.ClearRequest();
        }
    }

    #endregion
}