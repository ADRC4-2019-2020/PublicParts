using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenRecorder : MonoBehaviour
{
    public bool Record;
    private int _frame = 0;

    // Update is called once per frame
    void Update()
    {
        if (Record)
        {
            StartCoroutine(SaveScreenshot());
        }
    }

    IEnumerator SaveScreenshot()
    {
        string file = $"SavedFrames/TrainingSim/Frame_{_frame}.png";
        ScreenCapture.CaptureScreenshot(file);
        _frame++;
        yield return new WaitForEndOfFrame();
    }
}
