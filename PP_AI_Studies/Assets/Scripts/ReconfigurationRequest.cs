using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles a reconfiguration request and its results
/// </summary>
public class ReconfigurationRequest
{
    //The GUID of the space that needs to be reconfigured
    public Guid SpaceId { get; private set; }
    //The target parameters for the space
    public int TargetArea { get; private set; }
    public int TargetConnections { get; private set; }

    private int _areaModifier = 7;
    private int _connectivityModifier = 2;

    //Should store the agents involved in the reconfiguration here!

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

        //Set unfreeze the agents so they can make decisions
        foreach (var part in space.BoundaryParts)
        {
            part.CPAgent.UnfreezeAgent();
        }
    }

    /// <summary>
    /// Checks if the reconfiguration has been successful
    /// </summary>
    /// <param name="space">The space to be checked</param>
    /// <returns>The result if the reconfiguration was successful</returns>
    public bool ReconfigurationSuccessful(PPSpace space)
    {
        int currentArea = space.VoxelCount;
        int currentConnectivity = space.NumberOfConnections;

        bool areaSuccessful = true;
        bool connectivitySuccessful = true;
        
        if (TargetArea > 0 )
        {
            if (currentArea < TargetArea - 2 || currentArea > TargetArea + 2)
            {
                areaSuccessful = false;
            }
        }

        if (TargetConnections > 0)
        {
            if (currentConnectivity != TargetConnections)
            {
                connectivitySuccessful = false;
            }
        }

        if (areaSuccessful && connectivitySuccessful)
        {
            return true;
        }
        else return false;
    }
}
