using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


[System.Serializable]
public class ConfigurablePart : Part
{
    public ConfigurablePart() { }

    public ConfigurablePart NewPart(VoxelGrid grid)
    {
        //This method does not need to exist, the constructor is enough
        //Check PPSpace and PPSpaceRequest Json reading methods when implementing new reading method
        //This is probably creating double the amount of spaces 
        ConfigurablePart p = new ConfigurablePart();
        p.Type = PartType.Configurable;
        p.Orientation = (PartOrientation)System.Enum.Parse(typeof(PartOrientation), OrientationName, false);
        p.Grid = grid;
        p.IsStatic = false;
        p.Height = Height;

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


    public ConfigurablePart (VoxelGrid grid, List<Part> existingParts, int seed)
    {
        //This constructor creates a random configurable part in the specified grid. 
        Type = PartType.Configurable;
        Grid = grid;
        int minimumDistance = 6; //In voxels
        Size = new Vector2Int(6, 2); //6 x 2 configurable part size SHOULD NOT BE HARD CODED
        nVoxels = Size.x * Size.y;
        OccupiedIndexes = new Vector3Int[nVoxels];
        IsStatic = false;
        Height = 6;

        Random.InitState(seed);
        bool validPart = false;
        while (!validPart)
        {
            Orientation = (PartOrientation)Random.Range(0, 2);
            int randomX = Random.Range(0, Grid.Size.x - 1);
            int randomY = Random.Range(0, Grid.Size.y - 1);
            int randomZ = Random.Range(0, Grid.Size.z - 1);
            ReferenceIndex = new Vector3Int(randomX, randomY, randomZ);

            bool allInside = true;

            GetOccupiedIndexes();
            //Validate itself
            foreach (var index in OccupiedIndexes)
            {
                if (index.x >= Grid.Size.x || index.y >= Grid.Size.y || index.z >= Grid.Size.z)
                {
                    allInside = false;
                    break;
                }
                else if (Grid.Voxels[index.x, index.y, index.z].IsOccupied || !Grid.Voxels[index.x, index.y, index.z].IsActive)
                {
                    allInside = false;
                    break;
                }
            }

            //Validate on plan
            if (allInside)
            {
                if (!CheckValidDistance()) continue;
                else validPart = true;
            }
        }
        OccupyVoxels();
    }
    bool CheckValidDistance()
    {
        //Set the search sides according to index parity, even = negative, odd = positive
        Vector3Int[] evenSide = OccupiedIndexes.Where((x, i) => i % 2 == 0).ToArray();
        Vector3Int[] oddSide = OccupiedIndexes.Where((x, i) => i % 2 != 0).ToArray();

        //Set the search ranges according to part orientation, for boundary and for parallel parts and agnostic parts
        int boundaryRange = 4;
        int partsRange = 6;

        //Check, for both sides and both ranges if there is any impediment
        if (Orientation == PartOrientation.Horizontal)
        {
            //Try checking on both, starting from the even side, avoiding duplicates
            int[] pRange = new int[] { evenSide[0].z - partsRange, evenSide[0].z + partsRange + 1 };
            int[] bRange = new int[] { evenSide[0].z - boundaryRange - 1, evenSide[0].z + boundaryRange + 2 };
            foreach (var index in evenSide)
            {
                for (int z = pRange[0]; z <= pRange[1]; z++)
                {
                    //Skip its own indexes
                    if (z == index.z || z == index.z + 1) continue;
                    
                    //Return false if outside the grid
                    if (z < 0 || z > Grid.Size.z - 1) return false;
                    
                    Voxel checkVoxel = Grid.Voxels[index.x, index.y, z];
                    
                    //Return false if, within boundary search range, the voxel is not active (avoid perpendicular boundary)
                    if (z >= bRange[0] && z <= bRange[1])
                    {
                        if (!checkVoxel.IsActive) return false;
                    }

                    //Return false if a part is found and is not perpendicular
                    if (checkVoxel.IsOccupied && checkVoxel.Part.Orientation != PartOrientation.Vertical)
                    {
                        return false;
                    }
                }
            }
        }

        if (Orientation == PartOrientation.Vertical)
        {
            //Try checking on both, starting from the even side, avoiding duplicates
            int[] pRange = new int[] { evenSide[0].x - partsRange, evenSide[0].x + partsRange + 1 };
            int[] bRange = new int[] { evenSide[0].x - boundaryRange - 1, evenSide[0].x + boundaryRange + 2 };
            
            foreach (var index in evenSide)
            {
                for (int x = pRange[0]; x <= pRange[1]; x++)
                {
                    //Skip its own indexes
                    if (x == index.x || x == index.x + 1) continue;
                    
                    //Return false if outside the grid
                    if (x < 0 || x > Grid.Size.x - 1) return false;
                    
                    Voxel checkVoxel = Grid.Voxels[x, index.y, index.z];

                    //Return false if, within boundary search range, the voxel is not active (avoid perpendicular boundary)
                    if (x >= bRange[0] && x <= bRange[1])
                    {
                        if (!checkVoxel.IsActive) return false;
                    }

                    //Return false if a part is found and is not perpendicular
                    if (checkVoxel.IsOccupied && checkVoxel.Part.Orientation != PartOrientation.Horizontal)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    void GetOccupiedIndexes()
    {
        //This can be refactored
        //Check orientation and switch Size.x and Size.y as 
        //Longitudinal and transversal accordingly
        if (Orientation == PartOrientation.Horizontal)
        {
            int i = 0;
            for (int x = 0; x < Size.x; x++)
            {
                for (int z = 0; z < Size.y; z++)
                {
                    OccupiedIndexes[i++] = new Vector3Int(ReferenceIndex.x + x, ReferenceIndex.y, ReferenceIndex.z + z);
                }
            }

        }
        else if (Orientation == PartOrientation.Vertical)
        {
            int i = 0;
            for (int x = 0; x < Size.y; x++)
            {
                for (int z = 0; z < Size.x; z++)
                {
                    OccupiedIndexes[i++] = new Vector3Int(ReferenceIndex.x + x, ReferenceIndex.y, ReferenceIndex.z + z);
                }
            }
        }
    }
}
