using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Knot : Part
{
    public Knot(VoxelGrid grid)
    {
        Type = PartType.Knot;
        Orientation = PartOrientation.Agnostic;
        Grid = grid;
        IsStatic = true;
        Height = 6;
        
        var indexes = OCIndexes.Split(';');
        nVoxels = indexes.Length;
        OccupiedIndexes = new Vector3Int[nVoxels];
        OccupiedVoxels = new Voxel[nVoxels];

        for (int i = 0; i < nVoxels; i++)
        {
            var index = indexes[i];
            var coords = index.Split('_');
            Vector3Int vector = new Vector3Int(int.Parse(coords[0]), int.Parse(coords[1]), int.Parse(coords[2]));
            var voxel = grid.Voxels[vector.x, vector.y, vector.z];
            voxel.IsOccupied = true;
            voxel.Part = this;
            OccupiedIndexes[i] = vector;
            OccupiedVoxels[i] = voxel;

        }
        ReferenceIndex = OccupiedIndexes[0];
        CalculateCenter();
    }

    public Knot() { }

    public void ValidadeKnot(VoxelGrid grid)
    {
        Type = PartType.Knot;
        Orientation = PartOrientation.Agnostic;
        Grid = grid;
        IsStatic = true;
        Height = 6;

        var indexes = OCIndexes.Split(';');
        nVoxels = indexes.Length;
        OccupiedIndexes = new Vector3Int[nVoxels];
        OccupiedVoxels = new Voxel[nVoxels];

        for (int i = 0; i < nVoxels; i++)
        {
            var index = indexes[i];
            var coords = index.Split('_');
            Vector3Int vector = new Vector3Int(int.Parse(coords[0]), int.Parse(coords[1]), int.Parse(coords[2]));
            var voxel = grid.Voxels[vector.x, vector.y, vector.z];
            voxel.IsOccupied = true;
            voxel.Part = this;
            OccupiedIndexes[i] = vector;
            OccupiedVoxels[i] = voxel;

        }
        ReferenceIndex = OccupiedIndexes[0];
        CalculateCenter();
    }
}
