using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[System.Serializable]
public class PPSpaceRequest
{
    public Tenant Tenant; //Who is requesting
    public int Population; //In individuals
    public SpaceFunction Function; //Expected use of the space
    public int Duration; //In discrete hours
    public int StartTime; //In 24 hours
    public string ActivityName;
    public Dictionary<int,float> RequestProbability; //Percentage(float) per day(int) [MONDAY = 1]

    //Json reading properties
    public string TenantName;
    public string FunctionName;
    public string Probabilities_S; //Read as string separated by '_' and as integers (percentages)

}

[System.Serializable]
public class PPRequestCollection
{
    public PPSpaceRequest[] Requests;
}