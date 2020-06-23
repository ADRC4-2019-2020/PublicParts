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
    //The target parameters for the space
    public int TargetArea { get; private set; }
    public int TargetConnections { get; private set; }
    //The evaluation parameters, to add or remove accordingly
    private int _areaModifier = 8;
    private int _connectivityModifier = 2;
    //The components to be reconfigured
    private ConfigurablePartAgent[] _agents2Reconfigure;

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
        SpaceId = space.SpaceId;
        SpaceName = space.Name;
        int currentArea = space.VoxelCount;
        int currentConnectivity = space.NumberOfConnections;

        //Set the target area
        if (areaDirection == 1) TargetArea = currentArea + _areaModifier;
        else if (areaDirection == -1) TargetArea = currentArea - _areaModifier;
        else if (areaDirection == 0) TargetArea = 0;

        //Set the target connections
        if (connectivityDirection == 1) TargetConnections = currentConnectivity + _connectivityModifier;
        else if (connectivityDirection == -1) TargetConnections = currentConnectivity - _connectivityModifier;
        else if (connectivityDirection == 0) TargetConnections = 0;

        Debug.Log($"Reconfiguration requested for {space.Name}. Area from {currentArea} to {TargetArea}");

        _agents2Reconfigure = space.BoundaryParts.Select(p => p.CPAgent).ToArray();
        foreach (var part in _agents2Reconfigure)
        {
            part.SetRequest(this);
        }
        UnfreezeRandomAgent();
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
        //Get the current parameters
        int currentArea = space.VoxelCount;
        int currentConnectivity = space.NumberOfConnections;

        //Create the result validators
        bool areaSuccessful = true;
        bool connectivitySuccessful = true;
        
        //Check if area objective has been reached [0 = no area reconfiguration requested]
        if (TargetArea > 0 )
        {
            if (currentArea < TargetArea - 4 || currentArea > TargetArea + 4)
            {
                areaSuccessful = false;
            }
        }

        //Check if connectivity objective has been reached [0 = no connectivity reconfiguration requested]
        if (TargetConnections > 0)
        {
            if (currentConnectivity != TargetConnections)
            {
                connectivitySuccessful = false;
            }
        }

        //Check if the request has been fullfiled
        if (areaSuccessful && connectivitySuccessful)
        {
            OnReconfigurationSuccessful();
            return true;
        }
        //If not, continue with request open
        else
        {
            UnfreezeRandomAgent();
            return false;
        }
    }

    #endregion

    #region Exterior objects methods

    /// <summary>
    /// Sets the <see cref="ConfigurablePartAgent"/>s of all the parts to be reconfigured ans Unfrozen
    /// </summary>
    private void UnfreezeAgents()
    {
        foreach (var part in _agents2Reconfigure)
        {
            //Unfreeze the agents so they can make decisions
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
        //Debug.Log($"Unfrozen part {_parts2Reconfigure[i].Name}, state is {_parts2Reconfigure[i].CPAgent.Frozen}");
    }

    /// <summary>
    /// Method to be called if the target space does not exist anymore
    /// </summary>
    public void OnSpaceDestruction()
    {
        FreezeAgents();
        foreach (var agnt in _agents2Reconfigure)
        {
            agnt.SetAsComplete(false);
        }
    }

    /// <summary>
    /// Method to be called if the reconfiguration for this request has been successful
    /// </summary>
    public void OnReconfigurationSuccessful()
    {
        FreezeAgents();
        foreach (var agnt in _agents2Reconfigure)
        {
            agnt.SetAsComplete(true);
        }
    }

    #endregion
}