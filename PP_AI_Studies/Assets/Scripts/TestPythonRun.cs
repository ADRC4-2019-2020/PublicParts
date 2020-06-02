using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Diagnostics;

public class TestPythonRun : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        CallPython();
    }

    // Update is called once per frame

    void CallPython()
    {
        //Start the python process
        var psi = new ProcessStartInfo();
        psi.FileName = @"C:\Program Files (x86)\Microsoft Visual Studio\Shared\Python37_64\python.exe";
        
        //Define the script
        var script = @"D:\GitRepo\PublicParts\PP_AI_Studies\PyScripts\Pix2Pix\python_test_unity.py";

        //Define the variables
        var content = "Hello World";

        //Send the arguments throught the process
        string[] args = new string[2];
        args[0] = script;
        args[1] = content;
        psi.Arguments = string.Format("{0} {1}", args[0], args[1]);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        string errors = "";
        string results = "";

        using(var process = Process.Start(psi))
        {
            errors = process.StandardError.ReadToEnd();
            results = process.StandardOutput.ReadToEnd();
        }
        print(errors);
        print(results);
    }
}
