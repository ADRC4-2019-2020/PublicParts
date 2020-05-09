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

    //Area preferences are stored in a linear, 2 instances array. [0] = min, [1] = max
    //This represents the area in square meters per person in the population occupying the space
    //public Dictionary<SpaceFunction, int[]> AreaPreferences = new Dictionary<SpaceFunction, int[]>();
    public Dictionary<SpaceFunction, float[]> AreaPreferences = new Dictionary<SpaceFunction, float[]>();

    //The preferred area per individual in a population in square meters
    //Inferred by program, to be updated over time
    //Only one parameter, disregarding multiple functions
    public float AreaPerIndInferred = 1f; 

    //Connectivity preferences are stored in a linear, 2 instances array. [0] = min, [1] = max
    //This represents the preffered Connectivity ratio of the space per function
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
        _userIcon.transform.localScale = _userIcon.transform.localScale * scale;
        _userIcon.SetActive(false);
        _userIcon.GetComponent<UserIcon>().SetTenant(this);
        Color c = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
        _userIcon.GetComponentInChildren<MeshRenderer>().material.SetColor("_Color", c);
    }

    public void SetSpaceToIcon(PPSpace space, VoxelGrid grid)
    {
        _userIcon.SetActive(true);
        _userIcon.GetComponent<UserIcon>().SetSpace(space, grid);
        //_userIcon.SetActive(true);
    }

    public void ReleaseIcon()
    {
        _userIcon.GetComponent<UserIcon>().ReleaseSpace();
        _userIcon.SetActive(false);
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