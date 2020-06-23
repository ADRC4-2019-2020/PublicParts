using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public static class CSVReader
{
    public static int[] ReadDWP(string fileName)
    {
        string file = Resources.Load<TextAsset>(fileName).text;
        var lines = file.Split('\n').ToArray();

        return lines.Select(l => Int32.Parse(l)).ToArray();
    }

    public static string[] ReadNames(string fileName)
    {
        string file = Resources.Load<TextAsset>(fileName).text;

        return file.Split('\n').ToArray();
    }

    public static void SetGridState(VoxelGrid grid, string filename)
    {
        string file = Resources.Load<TextAsset>(filename).text;
        var lines = file.Split('\n').ToArray();
        foreach (var line in lines)
        {
            var comps = line.Split('_').ToArray();
            var x = int.Parse(comps[0]);
            var y = int.Parse(comps[1]);
            var z = int.Parse(comps[2]);
            var b = bool.Parse(comps[3]);
            grid.Voxels[x, y, z].IsActive = b;
        }
    }

    public static Vector3[] ReadPoints(string fileName)
    {
        //Save your file in the Resources folder in Unity, so you can call it form here
        string file = Resources.Load<TextAsset>(fileName).text;
        var lines = file.Split('\n').ToArray();

        Vector3[] result = new Vector3[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            var comps = line.Split('_').ToArray();
            var x = float.Parse(comps[0]);
            var y = float.Parse(comps[1]);
            var z = float.Parse(comps[2]);

            Vector3 point = new Vector3(x, y, z);
            result[i] = point;

        }

        return result;
    }
}
