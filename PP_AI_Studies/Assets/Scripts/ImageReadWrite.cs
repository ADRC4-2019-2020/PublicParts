using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;
using System.Diagnostics;



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


    public static Texture2D ReadWriteAI(VoxelGrid grid, string prefix)
    {
        //Stopwatch stopwatch = new Stopwatch();
        //stopwatch.Start();
        string folder = @"D:\GitRepo\PublicParts\PP_AI_Studies\temp_sr";
        string fileName = folder + @"\" + prefix;
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

        //Clean folder before writing
        var dir = new DirectoryInfo(folder);
        foreach (FileInfo file in dir.GetFiles("*.png"))
        {
            file.Delete();
        }

        //Write file to temp path
        string path = fileName + ".png";
        using (MemoryStream memory = new MemoryStream())
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
            {
                byte[] data = outputImage.EncodeToPNG();
                fs.Write(data, 0, data.Length);
            }
        }
        
        //File.WriteAllBytes(path, data);
        string editFolder = _folder + @"\Assets\Resources\temp_sr";
        
        // Post process source
        PP_ImageProcessing.ResizeAndFitCanvas(folder, "4", "256", "256");

        //Send to Pix2Pix python script
        PP_ImageProcessing.AnalysePix2PixPython(folder);

        //Post process step 1: resize
        PP_ImageProcessing.RestoreOriginalSize(folder);

        //Post process step 2: analyse pixels pass 1
        var postprocessd = PP_ImageProcessing.PostProcessImage(folder);

        //Post process step 3: analyse pixels pass 2
        postprocessd = PP_ImageProcessing.PostProcessImage(folder);

        //Stop stopwatch
        //stopwatch.Stop();
        //var time = stopwatch.ElapsedMilliseconds;
        //UnityEngine.Debug.Log($"Time taken to process AI = {time} ms");

        return postprocessd;
    }
}