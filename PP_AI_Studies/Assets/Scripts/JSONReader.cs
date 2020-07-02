using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class JSONReader
{ 
    public static List<PPTask> ReadTasksAsList()
    {
        List<PPTask> outList = new List<PPTask>();
        string jsonString = Resources.Load<TextAsset>("Input Data/U_Tasks").text;
        TaskCollection taskList = JsonUtility.FromJson<TaskCollection>(jsonString);
        foreach (var task in taskList.Tasks)
        {
            PPTask t = new PPTask();
            t.TenantName = task.TenantName;
            t.NewTenant();
            t.TaskDay = task.TaskDay;
            t.TaskProbability = task.TaskProbability;
            t.TaskTime = task.TaskTime;
            t.TaskTitle = task.TaskTitle;
            outList.Add(t);
        }
        return outList;
    }

    public static List<PPSpaceRequest> ReadSpaceRequests(string file, List<Tenant> existingTenants)
    {
        //THE LIST OF TENANTS SHOULD BE RELATED TO THE LIST OF REQUESTS
        var existingTenantNames = existingTenants.Select(t => t.Name).ToList();

        List<PPSpaceRequest> requests = new List<PPSpaceRequest>();
        string jsonString = Resources.Load<TextAsset>(file).text;
        PPRequestCollection requestList = JsonUtility.FromJson<PPRequestCollection>(jsonString);

        foreach (var request in requestList.Requests)
        {
            request.Tenant = existingTenants[existingTenantNames.IndexOf(request.TenantName)];

            request.Function = (SpaceFunction)System.Enum.Parse(typeof(SpaceFunction), request.FunctionName, false);
            var probabilities = request.Probabilities_S.Split('_');
            request.RequestProbability = new Dictionary<int, float>();
            for (int i = 0; i < probabilities.Length; i++)
            {
                float probability = int.Parse(probabilities[i]) / 100.00f;
                request.RequestProbability.Add(i, probability);
            }
            requests.Add(request);
        }
        return requests;
    }

    public static List<Tenant> ReadTenantsWithPreferences(string file, VoxelGrid grid)
    {
        //THE LIST OF TENANTS SHOULD BE RELATED TO THE LIST OF REQUESTS
        //This is set up to read only area preferences so far. Updates must check for references
        //Only Work and Leisure space functions implemented
        List<Tenant> tenants = new List<Tenant>();
        string jsonString = Resources.Load<TextAsset>(file).text;
        TenantCollection tenantList = JsonUtility.FromJson<TenantCollection>(jsonString);
        foreach (var tenant in tenantList.Tenants)
        {
            tenant.AssociateGrid(grid);
            //Tenant Area Preferences
            tenant.AreaPreferences = new Dictionary<SpaceFunction, float[]>();
            var areaWorkPref = tenant.AreaPrefWork_S.Split('_').Select(p => float.Parse(p) / 10.00f).ToArray();
            var areaLeisurePref = tenant.AreaPrefLeisure_S.Split('_').Select(p => float.Parse(p) / 10.00f).ToArray();
            tenant.AreaPreferences.Add(SpaceFunction.Work, areaWorkPref);
            tenant.AreaPreferences.Add(SpaceFunction.Leisure, areaLeisurePref);

            //Tenant Connectivity Preferences
            tenant.ConnectivityPreferences = new Dictionary<SpaceFunction, float[]>();
            var connecWorkPref = tenant.ConnectivityPrefWork_S.Split('_').Select(p => float.Parse(p) / 100.00f).ToArray();
            var connecLeisurePref = tenant.ConnectivityPrefLeisure_S.Split('_').Select(p => float.Parse(p) / 100.00f).ToArray();
            //Debug.Log($"{tenant.Name} min work con {connecLeisurePref[0]}");
            tenant.ConnectivityPreferences.Add(SpaceFunction.Work, connecWorkPref);
            tenant.ConnectivityPreferences.Add(SpaceFunction.Leisure, connecLeisurePref);
            tenant.CreateUserIcon();
            tenants.Add(tenant);
        }

        return tenants;
    }

    public static List<StructuralPart> ReadStructureAsList(VoxelGrid grid, string file)
    {
        List<StructuralPart> outList = new List<StructuralPart>();
        string jsonString = Resources.Load<TextAsset>(file).text;
        SPartCollection partList = JsonUtility.FromJson<SPartCollection>(jsonString);
        foreach (var part in partList.Parts)
        {
            StructuralPart p = new StructuralPart();
            p.OCIndexes = part.OCIndexes;
            p.OrientationName = part.OrientationName;
            p.OccupiedIndexes = part.OccupiedIndexes;
            p.Height = part.Height;

            outList.Add(p.NewPart(grid));
        }
        return outList;
    }

    public static List<Knot> ReadKnotsAsList(VoxelGrid grid, string file)
    {
        List<Knot> outList = new List<Knot>();
        string  jsonString = Resources.Load<TextAsset>(file).text;
        KnotCollection knotsList = JsonUtility.FromJson<KnotCollection>(jsonString);
        foreach (var knot in knotsList.Parts)
        {
            knot.ValidadeKnot(grid);
            outList.Add(knot);
        }

        return outList;
    }

    public static List<ConfigurablePart> ReadConfigurablesAsList(VoxelGrid grid, string file)
    {
        List<ConfigurablePart> outList = new List<ConfigurablePart>();
        string jsonString = Resources.Load<TextAsset>(file).text;
        CPartCollection partList = JsonUtility.FromJson<CPartCollection>(jsonString);
        foreach (var part in partList.Parts)
        {
            ConfigurablePart p = new ConfigurablePart();
            p.OCIndexes = part.OCIndexes;
            p.OrientationName = part.OrientationName;
            p.OccupiedIndexes = part.OccupiedIndexes;
            p.Height = part.Height;

            outList.Add(p.NewPart(grid));
        }

        return outList;
    }

    public static List<PPSpace> ReadSpacesAsList(VoxelGrid grid, string file)
    {
        List<PPSpace> outList = new List<PPSpace>();
        string jsonString = Resources.Load<TextAsset>(file).text;
        PPSpaceCollection spaceList = JsonUtility.FromJson<PPSpaceCollection>(jsonString);
        int count = 0;
        foreach (var space in spaceList.Spaces)
        {
            PPSpace s = new PPSpace();
            s.OCIndexes = space.OCIndexes;
            outList.Add(s.NewSpace(grid, $"Space_{count++}"));
        }

        return outList;
    }
}
