using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[System.Serializable]
public class Part : System.IEquatable<Part>
{
    public VoxelGrid Grid;

    public string Name;
    public PartType Type;
    public Vector2Int Size;
    public bool IsStatic;
    public Vector3Int ReferenceIndex;
    public Vector3 PartPivot;
    public int Height;
    
    public Vector3Int[] OccupiedIndexes;
    public Vector3 Center => new Vector3(OccupiedIndexes.Average(c => (float)c.x),
        OccupiedIndexes.Average(c => (float)c.y),
        OccupiedIndexes.Average(c => (float)c.z));
    public Voxel[] OccupiedVoxels;
    public PartOrientation Orientation;
    public int nVoxels;

    public string OrientationName;
    public string OCIndexes;

    public Voxel OriginVoxel => Grid.Voxels[ReferenceIndex.x, ReferenceIndex.y, ReferenceIndex.z];

    public void OccupyVoxels()
    {
        OccupiedVoxels = new Voxel[nVoxels];
        for (int i = 0; i < nVoxels; i++)
        {
            var index = OccupiedIndexes[i];
            Voxel voxel = Grid.Voxels[index.x, index.y, index.z];
            voxel.IsOccupied = true;
            voxel.Part = this;
            OccupiedVoxels[i] = voxel;
        }
        //CalculateCenter();
    }
    protected void CalculateCenter()
    {
        float avgX = OccupiedVoxels.Select(v => v.Center).Average(c => c.x);
        float avgY = OccupiedVoxels.Select(v => v.Center).Average(c => c.y);
        float avgZ = OccupiedVoxels.Select(v => v.Center).Average(c => c.z);
        //Center = new Vector3(avgX, avgY, avgZ);
    }


    public bool Equals(Part other)
    {
        return (other != null) && (Type == other.Type) && (Center == other.Center);
    }

    public override int GetHashCode()
    {
        return Type.GetHashCode() + Center.GetHashCode() + Orientation.GetHashCode();
    }
}
public class PartCollection
{
    public Part[] Parts;
}

public class CPartCollection
{
    public ConfigurablePart[] Parts;
}

public class SPartCollection
{
    public StructuralPart[] Parts;
}

public class KnotCollection
{
    public Knot[] Parts;
}