using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StructuralPart : Part
{
    public StructuralPart NewPart(VoxelGrid grid)
    {
        StructuralPart p = new StructuralPart();
        p.Type = PartType.Structure;
        p.Orientation = (PartOrientation)System.Enum.Parse(typeof(PartOrientation), OrientationName, false);
        p.Grid = grid;
        p.IsStatic = true;
        p.Height = 6;

        p.OCIndexes = OCIndexes;
        var indexes = p.OCIndexes.Split(';');
        p.nVoxels = indexes.Length;
        p.OccupiedIndexes = new Vector3Int[p.nVoxels];
        p.OccupiedVoxels = new Voxel[p.nVoxels];

        for (int i = 0; i < p.nVoxels; i++)
        {
            var index = indexes[i];
            var coords = index.Split('_');
            Vector3Int vector = new Vector3Int(int.Parse(coords[0]), int.Parse(coords[1]), int.Parse(coords[2]));
            var voxel = grid.Voxels[vector.x, vector.y, vector.z];
            voxel.IsOccupied = true;
            voxel.Part = p;
            p.OccupiedIndexes[i] = vector;
            p.OccupiedVoxels[i] = voxel;

        }
        p.ReferenceIndex = p.OccupiedIndexes[0];
        p.CalculateCenter();
        return p;
    }
}

