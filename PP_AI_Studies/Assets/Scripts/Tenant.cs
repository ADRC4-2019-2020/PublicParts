using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

[System.Serializable]
public class Tenant : IEquatable<Tenant>
{
    //
    //FIELDS AND PROPERTIES
    //
    public string Name;
    GameObject _userIcon;
    VoxelGrid _grid;
    public PPSpace OnSpace;

    /// <summary>
    /// Area preferences are stored in a linear, 2 floats array. [0] = min, [1] = max
    /// This represents the area in square meters per person in the population occupying the space
    /// </summary>
    public Dictionary<SpaceFunction, float[]> AreaPreferences = new Dictionary<SpaceFunction, float[]>();

    /// <summary>
    /// The preferred area per individual in a population in square meters. 
    /// Inferred by program, to be updated over time, only one parameter, disregarding multiple functions
    /// </summary>
    public float AreaPerIndInferred = 1f;


    /// <summary>
    /// Connectivity preferences are stored in a linear, 2 floats array. [0] = min, [1] = max
    /// This represents the preffered Connectivity ratio of the space per function
    /// </summary>
    public Dictionary<SpaceFunction, float[]> ConnectivityPreferences = new Dictionary<SpaceFunction, float[]>();

    public string AreaPrefWork_S;
    public string AreaPrefLeisure_S;
    public string ConnectivityPrefWork_S;
    public string ConnectivityPrefLeisure_S;

    //
    //METHODS AND CONSTRUCTORS
    //
    public void CreateUserIcon()
    {
        float scale = _grid.VoxelSize;
        _userIcon = GameObject.Instantiate(Resources.Load<GameObject>("GameObjects/UserIcon"));
        _userIcon.name = Name;
        _userIcon.transform.localScale = _userIcon.transform.localScale * scale;
        _userIcon.transform.SetParent(_grid.GridGO.transform.parent);
        _userIcon.GetComponent<UserIcon>().SetTenant(this);
        //Color c = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
        Color c = Color.black;
        _userIcon.GetComponentInChildren<MeshRenderer>().material.SetColor("_BaseColor", c);
        SetGOVisibility(false);
    }

    public void SetSpaceToIcon(PPSpace space, VoxelGrid grid)
    {
        SetGOVisibility(true);
        _userIcon.GetComponent<UserIcon>().SetSpace(space, grid);
        OnSpace = space;
    }

    public void SetGOVisibility(bool visible)
    {
        MeshRenderer mRenderer = _userIcon.transform.GetChild(0).GetComponent<MeshRenderer>();
        mRenderer.enabled = visible;
    }

    public void ReleaseIcon()
    {
        OnSpace = null;
        _userIcon.GetComponent<UserIcon>().ReleaseSpace();
        SetGOVisibility(false);
        _userIcon.transform.position = Vector3.zero;
    }
    
    public void AssociateGrid(VoxelGrid grid)
    {
        _grid = grid;
    }

    //Equality checking
    public bool Equals(Tenant other)
    {
        return (other != null && other.Name == Name);
    }
    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }
}

[System.Serializable]
public class TenantCollection
{
    public Tenant[] Tenants;
}