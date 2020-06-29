using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class ScreenRecorder : MonoBehaviour
{
    public bool Record;
    private int _frame = 0;
    public string FolderName;

    void Awake()
    {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, @"..\")) + @"SavedFrames\" + FolderName;
        //Create the directory if it doesn't exist
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
    }

    void Update()
    {
        if (Record) StartCoroutine(SaveScreenshot());
    }

    IEnumerator SaveScreenshot()
    {
        string file = $"SavedFrames/{FolderName}/Frame_{_frame}.png";
        ScreenCapture.CaptureScreenshot(file);
        _frame++;
        yield return new WaitForEndOfFrame();
    }
}
