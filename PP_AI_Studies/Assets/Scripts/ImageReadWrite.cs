using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


public static class ImageReadWrite
{
    //static Texture2D _outputImage;
    static string _folder = System.IO.Directory.GetCurrentDirectory();

    public static void WriteGrid2Image(VoxelGrid grid, int counter)
    {
        string fileName = $"/OutputPlans/Plan_{counter}";
        Vector2Int size = new Vector2Int(grid.Size.x, grid.Size.z);
        Texture2D outputImage = new Texture2D(size.x, size.y);
        for (int i = 0; i < size.x; i++)
        {
            for (int j = 0; j < size.y; j++)
            {
                var voxel = grid.Voxels[i, 0, j];
                Color c = !voxel.IsActive || voxel.IsOccupied ? Color.black : Color.white;
                outputImage.SetPixel(i, j, c);
                
            }
        }
        outputImage.Apply();
        byte[] data = outputImage.EncodeToPNG();
        string path = _folder + fileName + ".png";
        File.WriteAllBytes(path, data);
    }

    public static void WriteGrid2Image(VoxelGrid grid, int counter, string prefix)
    {
        string fileName = $"/TrainingPlans/{prefix}_{counter}";
        Vector2Int size = new Vector2Int(grid.Size.x, grid.Size.z);
        Texture2D outputImage = new Texture2D(size.x, size.y);
        for (int i = 0; i < size.x; i++)
        {
            for (int j = 0; j < size.y; j++)
            {
                var voxel = grid.Voxels[i, 0, j];
                Color c = !voxel.IsActive || voxel.IsOccupied ? Color.black : Color.white;
                outputImage.SetPixel(i, j, c);

            }
        }
        outputImage.Apply();
        byte[] data = outputImage.EncodeToPNG();
        string path = _folder + fileName + ".png";
        File.WriteAllBytes(path, data);
    }
}
