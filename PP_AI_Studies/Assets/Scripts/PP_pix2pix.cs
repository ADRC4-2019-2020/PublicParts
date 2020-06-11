using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Barracuda;

public class PP_pix2pix
{
    /// <summary>
    /// This is the class responsible for doing inferences
    /// from the pix2pix model saved from Tensorflow
    /// inside Unity, through Barracuda
    /// </summary>

    //Fields and parameters
    NNModel _modelAsset;
    Model _loadedModel;
    IWorker _worker;

    public PP_pix2pix()
    {
        //Class constructor, loads model and creates worker
        _modelAsset = Resources.Load<NNModel>("Models/pp_p2p_k2o");
        _loadedModel = ModelLoader.Load(_modelAsset);
        _worker = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, _loadedModel);
    }


    //Main methods
    public Texture2D GeneratePrediction(Texture2D inputTexture)
    {
        Debug.Log(inputTexture.format);
        //Create input tensor
        var tensor = new Tensor(inputTexture, channels: 3);
        //Normalize the tensor into [-1,1]
        var normalizedTensor = NormalizeTensor(tensor);
        //Execute the worker on the normalized tensor
        _worker.Execute(normalizedTensor);
        //Pull the output tensor from the worker
        var outputTensor = _worker.PeekOutput();
        //Normalize the output tensor
        var outputNormalized = NormalizeTensorUp(outputTensor);
        //Apply output tensor to a temp RenderTexture
        var tempRT = new RenderTexture(256, 256, 32);
        outputNormalized.ToRenderTexture(tempRT);
        RenderTexture.active = tempRT;
        //Assign temp RenderTexture to a new Texture2D
        var resultTexture = new Texture2D(inputTexture.width, inputTexture.height);
        resultTexture.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
        resultTexture.Apply();
        //Destroy temp RenderTexture
        RenderTexture.active = null;
        tempRT.DiscardContents();
        return resultTexture;
    }

    //Auxiliary methods
    Tensor NormalizeTensor(Tensor inputTensor)
    {
        //Prepare tensor data to be passed to pix2pix
        //formating floats from [0, 1] to [-1, 1]
        var data = inputTensor.data.Download(inputTensor.shape);
        float[] normalized = new float[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            normalized[i] = Normalize(data[i]);
        }

        return new Tensor(inputTensor.shape, normalized);
    }

    Tensor NormalizeTensorUp(Tensor inputTensor)
    {
        //Reverse the normalization of the tensor,
        //preparing to be parsed into image data,
        //formating floats from [-1, 1] to [0, 1]
        var data = inputTensor.data.Download(inputTensor.shape);
        //var data = inputTensor.AsFloats();
        float[] normalized = new float[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            normalized[i] = Normalize(data[i], -1f, 1f, 0f, 1f);
        }

        return new Tensor(inputTensor.shape, normalized);
    }

    float Normalize(float v, float a1, float a2, float b1, float b2)
    {
        float result = b1 + (v - a1) * (b2 - b1) / (a2 - a1);

        return result;
    }

    float Normalize(float v)
    {
        float a1 = 0f;
        float a2 = 1f;
        float b1 = -1f;
        float b2 = 1f;
        float result = b1 + (v - a1) * (b2 - b1) / (a2 - a1);

        return result;
    }

    Texture2D Tensor2Image(Tensor input)
    {
        //var data = input.data.Download(input.shape);
        var data = input.AsFloats();
        Color[] resultColors = new Color[256 * 256];
        for (int i = 0; i < resultColors.Length; i++)
        {
            var r = data[i * 3 + 0];
            var g = data[i * 3 + 1];
            var b = data[i * 3 + 2];

            Color c = new Color(r, g, b);
            resultColors[i] = c;
        }
        var result = new Texture2D(256, 256);
        result.SetPixels(resultColors);
        result.Apply();
        return result;
    }
}
