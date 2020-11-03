using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;
using System.Diagnostics;
using UnityEngine.UI;
using UnityEditor;

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

    /// <summary>
    /// Generates a texture from the grid, resized properly. This method it targeted to the original dataset,
    /// that had the error caused by the buggy resize, requiring the external resizing to be applied
    /// </summary>
    /// <param name="grid">The source <see cref="VoxelGrid"/></param>
    /// <returns>The result <see cref="Texture2D"/></returns>
    public static Texture2D TextureFromGridOriginal(VoxelGrid grid)
    {
        string folder = @"D:\GitRepo\PublicParts\PP_AI_Studies\temp_en";
        var textureFormat = TextureFormat.RGB24;
        
        Vector2Int size = new Vector2Int(grid.Size.x, grid.Size.z);
        //Image with extra border +1 to avoid the resizing cut problem
        Texture2D gridImage = new Texture2D(size.x + 1, size.y + 1, textureFormat, true, true);
        //Texture2D gridImage = new Texture2D(size.x, size.y);
        for (int i = 0; i < gridImage.width; i++)
        {
            for (int j = 0; j < gridImage.height; j++)
            {
                Color c;
                if (i == 0 || j == gridImage.height - 1)
                {
                    c = Color.green;
                }
                else
                {
                    var voxel = grid.Voxels[i - 1, 0, j];
                    c = !voxel.IsActive || voxel.IsOccupied ? Color.black : Color.white;
                }
                
                gridImage.SetPixel(i, j, c);
            }
        }
        gridImage.Apply();

        ////Construct the resulting resized image
        ////Scale image up, multiplying by 4
        //TextureScale.Point(gridImage, gridImage.width * 4, gridImage.height * 4);
        //Texture2D resultImage = new Texture2D(256, 256, textureFormat, true, true);
        ////Texture2D resultImage = new Texture2D(256, 256);
        ////Set all pixels to gray
        //Color[] grayPixels = new Color[256 * 256];
        //var gray = Color.gray;
        //for (int i = 0; i < grayPixels.Length; i++)
        //{
        //    grayPixels[i] = gray;
        //}
        //resultImage.SetPixels(grayPixels);
        //resultImage.Apply();

        ////Write grid image on result image
        //for (int i = 0; i < gridImage.width; i++)
        //{
        //    for (int j = 0; j < gridImage.height; j++)
        //    {
        //        int x = i;
        //        int y = resultImage.height - gridImage.height + j;
        //        resultImage.SetPixel(x, y, gridImage.GetPixel(i, j));
        //    }
        //}
        //resultImage.Apply();

        //temporarily save image to ensure encoding
        string path = folder + "/temp.png";
        using (MemoryStream memory = new MemoryStream())
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
            {
                byte[] data = gridImage.EncodeToPNG();
                fs.Write(data, 0, data.Length);
            }
        }
        //External resize and enlarging
        PP_ImageProcessing.ResizeAndFitCanvas(folder, "4", "256", "256");
        //ImageReadWrite.SaveImage2Path(gridImage, folder + @"\helpers\firstOutput");

        //Read externally resized image
        Texture2D image = new Texture2D(256, 256, textureFormat, true, true);

        byte[] imageData = File.ReadAllBytes(path);
        image.LoadImage(imageData);
        //UnityEngine.Debug.Log(image.width);

        return image;
    }

    /// <summary>
    /// Generates a texture from the grid, resized properly internally.
    /// </summary>
    /// <param name="grid">The source <see cref="VoxelGrid"/></param>
    /// <returns>The result <see cref="Texture2D"/></returns>
    public static Texture2D TextureFromGrid256(VoxelGrid grid)
    {
        //string folder = @"D:\GitRepo\PublicParts\PP_AI_Studies\temp_en";
        var textureFormat = TextureFormat.RGB24;

        Vector2Int size = new Vector2Int(grid.Size.x, grid.Size.z);
        Texture2D gridImage = new Texture2D(size.x, size.y, textureFormat, true, true);
        for (int i = 0; i < gridImage.width; i++)
        {
            for (int j = 0; j < gridImage.height; j++)
            {
                var voxel = grid.Voxels[i, 0, j];
                Color c = !voxel.IsActive || voxel.IsOccupied ? Color.black : Color.white;
                gridImage.SetPixel(i, j, c);
            }
        }
        gridImage.Apply();
        //ImageReadWrite.SaveImage2Path(gridImage, folder + @"\helpers\firstOutput");

        //Construct the resulting resized image
        //Scale image up, multiplying by 4
        TextureScale.Point(gridImage, gridImage.width * 4, gridImage.height * 4);
        //Texture2D resultImage = new Texture2D(256, 256, textureFormat, true, true);
        Texture2D resultImage = new Texture2D(256, 256);
        //Set all pixels to gray
        Color[] grayPixels = new Color[256 * 256];
        var gray = Color.gray;
        for (int i = 0; i < grayPixels.Length; i++)
        {
            grayPixels[i] = gray;
        }
        resultImage.SetPixels(grayPixels);
        resultImage.Apply();

        //Write grid image on result image
        for (int i = 0; i < gridImage.width; i++)
        {
            for (int j = 0; j < gridImage.height; j++)
            {
                int x = i;
                int y = resultImage.height - gridImage.height + j;
                resultImage.SetPixel(x, y, gridImage.GetPixel(i, j));
            }
        }
        resultImage.Apply();

        return resultImage;
    }

    /// <summary>
    /// Translates the current state of a grid into a RenderTexture
    /// </summary>
    /// <param name="grid">The <see cref="VoxelGrid"/> to translate</param>
    /// <returns>The resulting <see cref="RenderTexture"/></returns>
    public static RenderTexture RenderTextureFromGrid64(VoxelGrid grid)
    {
        var textureFormat = TextureFormat.RGB24;

        Vector2Int size = new Vector2Int(grid.Size.x, grid.Size.z);
        Texture2D gridImage = new Texture2D(size.x, size.y, textureFormat, true, true);
        for (int i = 0; i < gridImage.width; i++)
        {
            for (int j = 0; j < gridImage.height; j++)
            {
                var voxel = grid.Voxels[i, 0, j];
                Color c = !voxel.IsActive || voxel.IsOccupied ? Color.black : Color.white;
                gridImage.SetPixel(i, j, c);
            }
        }
        gridImage.Apply();

        Texture2D resultImage = new Texture2D(64, 64);
        //Set all pixels to gray
        Color[] grayPixels = new Color[64 * 64];
        var gray = Color.gray;
        for (int i = 0; i < grayPixels.Length; i++) grayPixels[i] = gray;
        resultImage.SetPixels(grayPixels);
        resultImage.Apply();

        //Write grid image on result image
        for (int i = 0; i < gridImage.width; i++)
        {
            for (int j = 0; j < gridImage.height; j++)
            {
                int x = i;
                int y = resultImage.height - gridImage.height + j;
                resultImage.SetPixel(x, y, gridImage.GetPixel(i, j));
            }
        }
        resultImage.Apply();

        RenderTexture destiny = new RenderTexture(64, 64, 0);
        Graphics.Blit(resultImage, destiny);

        return destiny;
    }

    public static Texture2D ReadWriteAI(VoxelGrid grid, string prefix)
    {
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
        //string editFolder = _folder + @"\Assets\Resources\temp_sr";
        
        // Post process source
        PP_ImageProcessing.ResizeAndFitCanvas(folder, "4", "256", "256");

        //Send to Pix2Pix python script
        PP_ImageProcessing.AnalysePix2PixPython(folder);

        //Post process step 1: resize
        PP_ImageProcessing.RestoreOriginalSize(folder);

        //Post process step 2: analyse pixels pass 1
        var postprocessd = PP_ImageProcessing.PostProcessImageFromFolder(folder);

        //Post process step 3: analyse pixels pass 2
        postprocessd = PP_ImageProcessing.PostProcessImageFromFolder(folder);

        //Stop stopwatch
        //stopwatch.Stop();
        //var time = stopwatch.ElapsedMilliseconds;
        //UnityEngine.Debug.Log($"Time taken to process AI = {time} ms");

        return postprocessd;
    }

    public static void SaveImage2Path(Texture2D image, string filePath)
    {
        //Write file to temp path
        string path = filePath + ".png";
        using (MemoryStream memory = new MemoryStream())
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
            {
                byte[] data = image.EncodeToPNG();
                fs.Write(data, 0, data.Length);
            }
        }
    }
}