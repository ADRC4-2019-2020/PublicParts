using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public class Voxel : IEquatable<Voxel>
{
    public Vector3Int Index;
    public Vector3 Center => _grid.Origin + new Vector3(Index.x + 0.5f, Index.y + 0.5f, Index.z + 0.5f) * _grid.VoxelSize;
    public bool IsOccupied;
    public bool IsActive;
    public List<Face> Faces = new List<Face>(6);
    public Part Part;
    public bool InSpace;
    private bool IsGridBoundary => Index.x == 0 || Index.x == _grid.Size.x - 1 || Index.z == 0 || Index.z == _grid.Size.z - 1;
    public bool IsBoundary => (GetFaceNeighbours().Any(n => !n.IsActive) || IsGridBoundary) && IsActive && !IsOccupied;
    public PPSpace ParentSpace;

    VoxelGrid _grid;

    public Voxel(Vector3Int index, VoxelGrid grid)
    {
        _grid = grid;
        Index = index;
        IsOccupied = false;
        IsActive = true;
    }
    /// <summary>
    /// Clean slate Constructor
    /// </summary>
    public Voxel() { }

    public IEnumerable<Voxel> GetFaceNeighbours()
    {
        int x = Index.x;
        int y = Index.y;
        int z = Index.z;
        var s = _grid.Size;

        if (x != 0) yield return _grid.Voxels[x - 1, y, z];
        if (x != s.x - 1) yield return _grid.Voxels[x + 1, y, z];

        if (y != 0) yield return _grid.Voxels[x, y - 1, z];
        if (y != s.y - 1) yield return _grid.Voxels[x, y + 1, z];

        if (z != 0) yield return _grid.Voxels[x, y, z - 1];
        if (z != s.z - 1) yield return _grid.Voxels[x, y, z + 1];
    }

    public List<Voxel> GetNeighboursInRange(int radius)
    {
        List<Voxel> neighbours = new List<Voxel>();
        int x = Index.x;
        int y = Index.y;
        int z = Index.z;
        var s = _grid.Size - Vector3Int.one;

        int xUnder = x - radius;
        int xUpper = x + radius;
        
        if (xUnder < 0) xUnder = 0;
        if (xUpper > s.x) xUpper = s.x;

        int zUnder = z - radius;
        int zUpper = z + radius;

        if (zUnder < 0) zUnder = 0;
        if (zUpper > s.z) zUpper = s.z;

        for (int i = xUnder; i < xUpper; i++)
        {
            for (int j = zUnder; j < zUpper; j++)
            {
                var n = _grid.Voxels[i, y, j];
                if(n.IsActive) neighbours.Add(n);
            }
        }
        return neighbours;
    }

    public PPSpace MoveToSpace(PPSpace target)
    {
        target.Voxels.Add(this);
        this.ParentSpace.Voxels.Remove(this);
        this.ParentSpace = target;

        return target;
    }

    public bool Equals(Voxel other)
    {
        return (other != null) && (Index == other.Index) && (IsOccupied == other.IsOccupied) && (IsActive == other.IsActive);
    }

    public override int GetHashCode()
    {
        return Index.GetHashCode() + IsActive.GetHashCode() + IsOccupied.GetHashCode();
    }

    //public Voxel DeepCopy()
    //{
    //    Voxel newVoxel = new Voxel(new Vector3Int(Index.x, Index.y, Index.z), _grid);

    //    newVoxel.IsOccupied = IsOccupied;
    //    newVoxel.IsActive = IsActive;
    //    newVoxel.Faces = Faces;
    //    newVoxel.Part = Part;
    //    newVoxel.InSpace = InSpace;

    //    return newVoxel;
    //}

}
