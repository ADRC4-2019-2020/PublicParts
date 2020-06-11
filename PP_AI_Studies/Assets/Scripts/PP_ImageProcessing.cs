using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class PP_ImageProcessing
{
    static string _imageResize = Path.GetFullPath(Path.Combine(Application.dataPath, @"..\")) + @"PP_Extensions\ImageResize\ImageResize.exe";
    public static void ResizeAndFitCanvas(string filePath, string resizeFactor, string width, string height)
    {
        var psi = new ProcessStartInfo();
        //psi.FileName = @"D:\GitRepo\ImageProcessingUnity\ImageResize\bin\Release\ImageResize.exe";
        psi.FileName = _imageResize;

        //Send the arguments through the process
        string[] args = new string[6];
        args[0] = "r";
        args[1] = filePath;
        args[2] = filePath /*+ @"\source"*/;
        args[3] = resizeFactor;
        args[4] = width;
        args[5] = height;
        psi.Arguments = string.Format("{0} {1} {2} {3} {4} {5}", args[0], args[1], args[2], args[3], args[4], args[5]);

        //Configure the process
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        string errors = "";
        string results = "";

        using (var process = Process.Start(psi))
        {
            process.EnableRaisingEvents = true;
            errors = process.StandardError.ReadToEnd();
            results = process.StandardOutput.ReadToEnd();
        }
        //Expose errors and results on log
        //UnityEngine.Debug.Log($"{errors}, {results}");
    }

    public static void AnalysePix2PixPython(string folder)
    {
        //Create the process
        var psi = new ProcessStartInfo();
        //Locate Python to execute the script
        psi.FileName = @"C:\Program Files (x86)\Microsoft Visual Studio\Shared\Python37_64\python.exe";

        //Set script file
        string script = @"D:\GitRepo\PublicParts\PP_AI_Studies\PyScripts\Pix2Pix\PP_p2p_Generate.py";

        //Send arguments through the process
        string[] args = new string[2];
        args[0] = script;
        args[1] = folder;
        psi.Arguments = string.Format("{0} {1}", args[0], args[1]);

        //Configure process
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        string errors = "";
        string results = "";

        using (var process = Process.Start(psi))
        {
            errors = process.StandardError.ReadToEnd();
            results = process.StandardOutput.ReadToEnd();
        }
        //Expose errors and results on Log
        //UnityEngine.Debug.Log(errors);
        //UnityEngine.Debug.Log(results);
    }

    public static void AnalysePix2PixExe(string folder)
    {
        //Create the process
        var psi = new ProcessStartInfo();
        //Locate Python to execute the script
        psi.FileName = @"D:\GitRepo\PublicParts\PP_AI_Studies\PyScripts\Pix2Pix\dist\PP_p2p_Generate\PP_p2p_Generate.exe";

        //Send the arguments throught the process
        string[] args = new string[1];
        args[0] = folder;

        psi.Arguments = string.Format("{0}", args[0]);

        //Configure process
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        string errors = "";
        string results = "";

        using (var process = Process.Start(psi))
        {
            errors = process.StandardError.ReadToEnd();
            results = process.StandardOutput.ReadToEnd();
        }
        UnityEngine.Debug.Log(errors);
        UnityEngine.Debug.Log(results);
    }
    public static void RestoreOriginalSize(string folder)
    {
        var psi = new ProcessStartInfo();
        psi.FileName = _imageResize;

        //Send the arguments through the process
        string[] args = new string[3];
        args[0] = "d";
        args[1] = folder;
        args[2] = folder;

        psi.Arguments = string.Format("{0} {1} {2}", args[0], args[1], args[2]);

        //Configure the process
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        string errors = "";
        string results = "";

        using (var process = Process.Start(psi))
        {
            process.EnableRaisingEvents = true;
            errors = process.StandardError.ReadToEnd();
            results = process.StandardOutput.ReadToEnd();
        }

        UnityEngine.Debug.Log($"{errors}, {results}");
    }

    public static void EncodePNG(string folder)
    {
        var psi = new ProcessStartInfo();
        psi.FileName = _imageResize;

        //Send the arguments through the process
        string[] args = new string[2];
        args[0] = "e";
        args[1] = folder;

        psi.Arguments = string.Format("{0} {1}", args[0], args[1]);

        //Configure the process
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        string errors = "";
        string results = "";

        using (var process = Process.Start(psi))
        {
            process.EnableRaisingEvents = true;
            errors = process.StandardError.ReadToEnd();
            results = process.StandardOutput.ReadToEnd();
        }
    }

    public static Texture2D PostProcessImageFromTexture(Texture2D image)
    {
        UnityEngine.Color red = UnityEngine.Color.red;
        UnityEngine.Color black = UnityEngine.Color.black;
        UnityEngine.Color white = UnityEngine.Color.white;
        UnityEngine.Color green = UnityEngine.Color.green;
        //string[] sourceImages = Directory.GetFiles(folder, "*.png");
        //var imageFile = sourceImages[0];


        //string fileName = Path.GetFileName(imageFile);
        //string[] name_noExtension = Path.GetFileNameWithoutExtension(imageFile).Split('_');

        int realWidth = image.width;
        int realHeight = image.height;

        Vector2Int gridSize = new Vector2Int(realWidth, realHeight);

        //Texture2D image = new Texture2D(realWidth, realHeight);

        //byte[] imageData = File.ReadAllBytes(imageFile);
        //image.LoadImage(imageData);

        //Normalize image here
        var pixels = image.GetPixels();
        var pixelsNormalized = new UnityEngine.Color[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            float r = Mathf.Round(pixel.r);
            float g = Mathf.Round(pixel.g);
            float b = Mathf.Round(pixel.b);
            float a = Mathf.Round(pixel.a);
            UnityEngine.Color c = new UnityEngine.Color(r, g, b, a);

            if (c != white && c != black) c = red;
            pixelsNormalized[i] = c;
        }
        image.SetPixels(pixelsNormalized);
        image.Apply();



        for (int i = 0; i < realWidth; i++)
        {
            for (int j = 0; j < realHeight; j++)
            {
                Vector2Int pixel = new Vector2Int(i, j);
                UnityEngine.Color pixelColor = image.GetPixel(i, j);

                //Collect pixel neighbours
                var neighbours = PixelNeighboursDouble(pixel, gridSize);

                var top1 = image.GetPixel(neighbours[0, 0].x, neighbours[0, 0].y);
                var top2 = image.GetPixel(neighbours[0, 1].x, neighbours[0, 1].y);

                var bottom1 = image.GetPixel(neighbours[1, 0].x, neighbours[1, 0].y);
                var bottom2 = image.GetPixel(neighbours[1, 1].x, neighbours[1, 1].y);

                var left1 = image.GetPixel(neighbours[2, 0].x, neighbours[2, 0].y);
                var left2 = image.GetPixel(neighbours[2, 1].x, neighbours[2, 1].y);

                var right1 = image.GetPixel(neighbours[3, 0].x, neighbours[3, 0].y);
                var right2 = image.GetPixel(neighbours[3, 1].x, neighbours[3, 1].y);

                UnityEngine.Color[] layer1 = { top1, bottom1, left1, right1 };
                UnityEngine.Color[] layer2 = { top2, bottom2, left2, right2 };

                if (pixelColor == white)
                {
                    //Clear 01 pixel gap
                    if (layer1.Count(p => p == black || p == red) == 2)
                    {
                        //Vertical
                        if ((top1 == black || top1 == red) && (bottom1 == black || bottom1 == red))
                        {
                            image.SetPixel(i, j, red);
                            image.Apply();
                        }

                        //Horizontal
                        else if ((left1 == black || left1 == red) && (right1 == black || right1 == red))
                        {
                            image.SetPixel(i, j, red);
                            image.Apply();
                        }
                    }

                    //Clar 02 pixels gap
                    else if ((layer1.Count(p => p == black) == 1) && (layer1.Count(n => n == red) == 0))
                    {
                        //Index of the black pixel
                        var n = Array.IndexOf(layer1, black);
                        //Direction to look at
                        int d;
                        if (n == 0) d = 1;
                        else if (n == 1) d = 0;
                        else if (n == 2) d = 3;
                        else d = 2;

                        if (layer1[d] == white && layer2[d] == black)
                        {
                            image.SetPixel(i, j, UnityEngine.Color.green);

                            var neighbour = neighbours[d, 0];
                            image.SetPixel(neighbour.x, neighbour.y, red);
                            image.Apply();
                        }
                    }


                }
                //else if (pixelColor == black)
                //{
                //    //Do Nothing
                //}
                else if (pixelColor == red)
                {
                    //Trace line until it reaches a boundary element, from red
                    if (layer1.Count(p => p == white) == 3 && layer1.Count(p => p == red) == 1)
                    {
                        //Index of the red pixel
                        var n = Array.IndexOf(layer1, red);

                        //Direction to move towards
                        int d = 0;
                        Vector2Int displace = new Vector2Int();
                        if (n == 0)
                        {
                            d = 1;
                            displace = new Vector2Int(0, -1);
                        }
                        else if (n == 1)
                        {
                            d = 0;
                            displace = new Vector2Int(0, 1);
                        }
                        else if (n == 2)
                        {
                            d = 3;
                            displace = new Vector2Int(1, 0);
                        }
                        else if (n == 3)
                        {
                            d = 2;
                            displace = new Vector2Int(-1, 0);
                        }

                        bool foundBoundary = false;
                        List<Vector2Int> newPixels = new List<Vector2Int>();
                        var currentPixel = pixel + displace;
                        int maxDistance = 20;
                        while (!foundBoundary && newPixels.Count <= maxDistance)
                        {
                            newPixels.Add(currentPixel);
                            var cpNeighbours = PixelNeighbours(currentPixel, gridSize);
                            for (int k = 0; k < cpNeighbours.Length; k++)
                            {
                                if (k != n)
                                {
                                    var neighbour = cpNeighbours[k];
                                    var nColor = image.GetPixel(neighbour.x, neighbour.y);
                                    if (nColor == black || nColor == red || nColor == null)
                                    {
                                        foundBoundary = true;
                                    }
                                }
                            }
                            currentPixel += displace;
                        }
                        foreach (var np in newPixels)
                        {
                            image.SetPixel(np.x, np.y, green);
                        }
                        image.Apply();
                    }
                    //Trace line until it reaches a boundary element, from black
                    else if (layer1.Count(p => p == white) == 3 && layer1.Count(p => p == black) == 1)
                    {
                        //Index of the red pixel
                        var n = Array.IndexOf(layer1, black);

                        //Direction to move towards
                        int d = 0;
                        Vector2Int displace = new Vector2Int();
                        if (n == 0)
                        {
                            d = 1;
                            displace = new Vector2Int(0, -1);
                        }
                        else if (n == 1)
                        {
                            d = 0;
                            displace = new Vector2Int(0, 1);
                        }
                        else if (n == 2)
                        {
                            d = 3;
                            displace = new Vector2Int(1, 0);
                        }
                        else if (n == 3)
                        {
                            d = 2;
                            displace = new Vector2Int(-1, 0);
                        }

                        bool foundBoundary = false;
                        List<Vector2Int> newPixels = new List<Vector2Int>();
                        var currentPixel = pixel + displace;
                        while (!foundBoundary)
                        {
                            newPixels.Add(currentPixel);
                            var cpNeighbours = PixelNeighbours(currentPixel, gridSize);
                            for (int k = 0; k < cpNeighbours.Length; k++)
                            {
                                if (k != n)
                                {
                                    var neighbour = cpNeighbours[k];
                                    var nColor = image.GetPixel(neighbour.x, neighbour.y);
                                    if (nColor == black || nColor == red)
                                    {
                                        foundBoundary = true;
                                    }
                                }
                            }
                            currentPixel += displace;
                        }
                        foreach (var np in newPixels)
                        {
                            image.SetPixel(np.x, np.y, green);
                        }
                        image.Apply();
                    }
                }
            }
        }

        //Fix temp green pixels
        var preview = image.GetPixels();
        var pixFixed = new UnityEngine.Color[preview.Length];
        for (int p = 0; p < preview.Length; p++)
        {
            var pix = preview[p];
            if (pix == green)
            {
                pix = red;
            }
            pixFixed[p] = pix;
        }
        image.SetPixels(pixFixed);
        image.Apply();

        //Write new file to folder
        //string path = folder + @"\" + fileName;
        //using (MemoryStream memory = new MemoryStream())
        //{
        //    using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
        //    {
        //        byte[] data = image.EncodeToPNG();
        //        fs.Write(data, 0, data.Length);
        //    }
        //}
        //UnityEngine.Debug.Log($"file written to {path}");

        return image;

    }

    public static Texture2D PostProcessImageFromFolder(string folder)
    {
        UnityEngine.Color red = UnityEngine.Color.red;
        UnityEngine.Color black = UnityEngine.Color.black;
        UnityEngine.Color white = UnityEngine.Color.white;
        UnityEngine.Color green = UnityEngine.Color.green;
        string[] sourceImages = Directory.GetFiles(folder, "*.png");
        var imageFile = sourceImages[0];


        string fileName = Path.GetFileName(imageFile);
        string[] name_noExtension = Path.GetFileNameWithoutExtension(imageFile).Split('_');

        int realWidth = int.Parse(name_noExtension[0]);
        int realHeight = int.Parse(name_noExtension[1]);

        Vector2Int gridSize = new Vector2Int(realWidth, realHeight);

        Texture2D image = new Texture2D(realWidth, realHeight);

        byte[] imageData = File.ReadAllBytes(imageFile);
        image.LoadImage(imageData);

        //Normalize image here
        var pixels = image.GetPixels();
        var pixelsNormalized = new UnityEngine.Color[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            float r = Mathf.Round(pixel.r);
            float g = Mathf.Round(pixel.g);
            float b = Mathf.Round(pixel.b);
            float a = Mathf.Round(pixel.a);
            UnityEngine.Color c = new UnityEngine.Color(r, g, b, a);

            if (c != white && c != black) c = red;
            pixelsNormalized[i] = c;
        }
        image.SetPixels(pixelsNormalized);
        image.Apply();



        for (int i = 0; i < realWidth; i++)
        {
            for (int j = 0; j < realHeight; j++)
            {
                Vector2Int pixel = new Vector2Int(i, j);
                UnityEngine.Color pixelColor = image.GetPixel(i, j);

                //Collect pixel neighbours
                var neighbours = PixelNeighboursDouble(pixel, gridSize);

                var top1 = image.GetPixel(neighbours[0, 0].x, neighbours[0, 0].y);
                var top2 = image.GetPixel(neighbours[0, 1].x, neighbours[0, 1].y);

                var bottom1 = image.GetPixel(neighbours[1, 0].x, neighbours[1, 0].y);
                var bottom2 = image.GetPixel(neighbours[1, 1].x, neighbours[1, 1].y);

                var left1 = image.GetPixel(neighbours[2, 0].x, neighbours[2, 0].y);
                var left2 = image.GetPixel(neighbours[2, 1].x, neighbours[2, 1].y);

                var right1 = image.GetPixel(neighbours[3, 0].x, neighbours[3, 0].y);
                var right2 = image.GetPixel(neighbours[3, 1].x, neighbours[3, 1].y);

                UnityEngine.Color[] layer1 = { top1, bottom1, left1, right1 };
                UnityEngine.Color[] layer2 = { top2, bottom2, left2, right2 };

                if (pixelColor == white)
                {
                    //Clear 01 pixel gap
                    if (layer1.Count(p => p == black || p == red) == 2)
                    {
                        //Vertical
                        if ((top1 == black || top1 == red) && (bottom1 == black || bottom1 == red))
                        {
                            image.SetPixel(i, j, red);
                            image.Apply();
                        }

                        //Horizontal
                        else if ((left1 == black || left1 == red) && (right1 == black || right1 == red))
                        {
                            image.SetPixel(i, j, red);
                            image.Apply();
                        }
                    }

                    //Clar 02 pixels gap
                    else if ((layer1.Count(p => p == black) == 1) && (layer1.Count(n => n == red) == 0))
                    {
                        //Index of the black pixel
                        var n = Array.IndexOf(layer1, black);
                        //Direction to look at
                        int d;
                        if (n == 0) d = 1;
                        else if (n == 1) d = 0;
                        else if (n == 2) d = 3;
                        else d = 2;

                        if (layer1[d] == white && layer2[d] == black)
                        {
                            image.SetPixel(i, j, UnityEngine.Color.green);

                            var neighbour = neighbours[d, 0];
                            image.SetPixel(neighbour.x, neighbour.y, red);
                            image.Apply();
                        }
                    }


                }
                //else if (pixelColor == black)
                //{
                //    //Do Nothing
                //}
                else if (pixelColor == red)
                {
                    //Trace line until it reaches a boundary element, from red
                    if (layer1.Count(p => p == white) == 3 && layer1.Count(p => p == red) == 1)
                    {
                        //Index of the red pixel
                        var n = Array.IndexOf(layer1, red);

                        //Direction to move towards
                        int d = 0;
                        Vector2Int displace = new Vector2Int();
                        if (n == 0)
                        {
                            d = 1;
                            displace = new Vector2Int(0, -1);
                        }
                        else if (n == 1)
                        {
                            d = 0;
                            displace = new Vector2Int(0, 1);
                        }
                        else if (n == 2)
                        {
                            d = 3;
                            displace = new Vector2Int(1, 0);
                        }
                        else if (n == 3)
                        {
                            d = 2;
                            displace = new Vector2Int(-1, 0);
                        }

                        bool foundBoundary = false;
                        List<Vector2Int> newPixels = new List<Vector2Int>();
                        var currentPixel = pixel + displace;
                        int maxDistance = 20;
                        while (!foundBoundary && newPixels.Count <= maxDistance)
                        {
                            newPixels.Add(currentPixel);
                            var cpNeighbours = PixelNeighbours(currentPixel, gridSize);
                            for (int k = 0; k < cpNeighbours.Length; k++)
                            {
                                if (k != n)
                                {
                                    var neighbour = cpNeighbours[k];
                                    var nColor = image.GetPixel(neighbour.x, neighbour.y);
                                    if (nColor == black || nColor == red || nColor == null)
                                    {
                                        foundBoundary = true;
                                    }
                                }
                            }
                            currentPixel += displace;
                        }
                        foreach (var np in newPixels)
                        {
                            image.SetPixel(np.x, np.y, green);
                        }
                        image.Apply();
                    }
                    //Trace line until it reaches a boundary element, from black
                    else if (layer1.Count(p => p == white) == 3 && layer1.Count(p => p == black) == 1)
                    {
                        //Index of the red pixel
                        var n = Array.IndexOf(layer1, black);

                        //Direction to move towards
                        int d = 0;
                        Vector2Int displace = new Vector2Int();
                        if (n == 0)
                        {
                            d = 1;
                            displace = new Vector2Int(0, -1);
                        }
                        else if (n == 1)
                        {
                            d = 0;
                            displace = new Vector2Int(0, 1);
                        }
                        else if (n == 2)
                        {
                            d = 3;
                            displace = new Vector2Int(1, 0);
                        }
                        else if (n == 3)
                        {
                            d = 2;
                            displace = new Vector2Int(-1, 0);
                        }

                        bool foundBoundary = false;
                        List<Vector2Int> newPixels = new List<Vector2Int>();
                        var currentPixel = pixel + displace;
                        while (!foundBoundary)
                        {
                            newPixels.Add(currentPixel);
                            var cpNeighbours = PixelNeighbours(currentPixel, gridSize);
                            for (int k = 0; k < cpNeighbours.Length; k++)
                            {
                                if (k != n)
                                {
                                    var neighbour = cpNeighbours[k];
                                    var nColor = image.GetPixel(neighbour.x, neighbour.y);
                                    if (nColor == black || nColor == red)
                                    {
                                        foundBoundary = true;
                                    }
                                }
                            }
                            currentPixel += displace;
                        }
                        foreach (var np in newPixels)
                        {
                            image.SetPixel(np.x, np.y, green);
                        }
                        image.Apply();
                    }
                }
            }
        }

        //Fix temp green pixels
        var preview = image.GetPixels();
        var pixFixed = new UnityEngine.Color[preview.Length];
        for (int p = 0; p < preview.Length; p++)
        {
            var pix = preview[p];
            if (pix == green)
            {
                pix = red;
            }
            pixFixed[p] = pix;
        }
        image.SetPixels(pixFixed);
        image.Apply();

        //Write new file to folder
        string path = folder + @"\" + fileName;
        using (MemoryStream memory = new MemoryStream())
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
            {
                byte[] data = image.EncodeToPNG();
                fs.Write(data, 0, data.Length);
            }
        }
        //UnityEngine.Debug.Log($"file written to {path}");

        return image;

    }

    static Vector2Int[] PixelNeighbours(Vector2Int pixel, Vector2Int gridSize)
    {
        //{top, bottom, left, right}
        Vector2Int[] neighbours = new Vector2Int[4];

        int x = pixel.x;
        int y = pixel.y;

        int width = gridSize.x;
        int height = gridSize.y;
        if (y + 1 < height)
        {
            Vector2Int top = new Vector2Int(x, y + 1);
            neighbours[0] = top;
        }
        if (y - 1 >= 0)
        {
            Vector2Int bottom = new Vector2Int(x, y - 1);
            neighbours[1] = bottom;
        }
        if (x - 1 >= 0)
        {
            Vector2Int left = new Vector2Int(x - 1, y);
            neighbours[2] = left;
        }
        if (x + 1 < width)
        {
            Vector2Int right = new Vector2Int(x + 1, y);
            neighbours[3] = right;
        }
        return neighbours;
    }

    static Vector2Int[,] PixelNeighboursDouble(Vector2Int pixel, Vector2Int gridSize)
    {
        //{top, bottom, left, right}
        Vector2Int[,] neighbours = new Vector2Int[4,2];

        int x = pixel.x;
        int y = pixel.y;

        int width = gridSize.x;
        int height = gridSize.y;
        
        //Get top pair
        if (y + 1 < height)
        {
            Vector2Int top1 = new Vector2Int(x, y + 1);
            neighbours[0, 0] = top1;
            if (y + 2 < height)
            {
                Vector2Int top2 = new Vector2Int(x, y + 2);
                neighbours[0, 1] = top2;
            }
        }
        
        //Get bottom pair
        if (y - 1 >= 0)
        {
            Vector2Int bottom1 = new Vector2Int(x, y - 1);
            neighbours[1,0] = bottom1;
            if (y - 2 >= 0)
            {
                Vector2Int bottom2 = new Vector2Int(x, y - 2);
                neighbours[1, 1] = bottom2;
            }
        }
        
        //Get left pair
        if (x - 1 >= 0)
        {
            Vector2Int left1 = new Vector2Int(x - 1, y);
            neighbours[2,0] = left1;
            if (x - 2 >= 0)
            {
                Vector2Int left2 = new Vector2Int(x - 2, y);
                neighbours[2, 1] = left2;
            }
        }
        
        //Get right pair
        if (x + 1 < width)
        {
            Vector2Int right1 = new Vector2Int(x + 1, y);
            neighbours[3, 0] = right1;
            if (x + 2 < width)
            {
                Vector2Int right2 = new Vector2Int(x + 2, y);
                neighbours[3, 1] = right2;
            }
        }
        return neighbours;
    }


}
