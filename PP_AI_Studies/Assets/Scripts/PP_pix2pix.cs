using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Barracuda;

/// <summary>
/// Responsible for doing inferences
/// from the pix2pix model saved from Tensorflow
/// inside Unity, through Barracuda
/// </summary>
public class PP_pix2pix
{
    #region Fields and parameters

    NNModel _modelAsset;
    Model _loadedModel;
    IWorker _worker;

    #endregion

    #region Constructor
    /// <summary>
    /// Constructs the PP_pix2pix engine, loading the model and creating the Worker responsible for
    /// inferring results
    /// </summary>
    public PP_pix2pix(string version)
    {
        //Class constructor, loads model and creates worker
        if (version == "original" )
        {
            _modelAsset = Resources.Load<NNModel>("Models/pp_p2p_k2o");
        }
        else if (version == "30x24")
        {
            _modelAsset = Resources.Load<NNModel>("Models/pp_p2p_v4_k2o");
        }
        _loadedModel = ModelLoader.Load(_modelAsset);
        _worker = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, _loadedModel);
    }

    #endregion

    #region Main methods

    /// <summary>
    /// Generates a prediction from an input Texture2D obeject
    /// </summary>
    /// <param name="inputTexture">The input texture to be transformed</param>
    /// <returns></returns>
    public Texture2D GeneratePrediction(Texture2D inputTexture)
    {
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

        Texture2D result = Tensor2Image(outputNormalized, inputTexture);

        tensor.Dispose();
        normalizedTensor.Dispose();
        outputNormalized.Dispose();
        outputTensor.Dispose();
        

        return result;
    }

    #endregion

    #region Auxiliary methods

    /// <summary>
    /// Normalizes an input tensor from (0, 1) to (-1, 1) to be processed by the Pix2Pix worker
    /// </summary>
    /// <param name="inputTensor">The input tensor, formated as (0, 1)</param>
    /// <returns>The output tensor, formated as (-1, 1)</returns>
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

    /// <summary>
    /// Undo the normalization step, formating the tensor from (-1, 1) to (0, 1)
    /// </summary>
    /// <param name="inputTensor">The input tensor, formated as (-1, 1)</param>
    /// <returns>The output tensor, formated as (0, 1)</returns>
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

    private float Normalize(float v, float a1, float a2, float b1, float b2)
    {
        float result = b1 + (v - a1) * (b2 - b1) / (a2 - a1);

        return result;
    }

    private float Normalize(float v)
    {
        float a1 = 0f;
        float a2 = 1f;
        float b1 = -1f;
        float b2 = 1f;
        float result = b1 + (v - a1) * (b2 - b1) / (a2 - a1);

        return result;
    }

    /// <summary>
    /// Translates a Tensor into a Texture2D
    /// </summary>
    /// <param name="inputTensor">The Tensor to be translated</param>
    /// <param name="inputTexture">A reference Texture2D for formatting</param>
    /// <returns></returns>
    Texture2D Tensor2Image(Tensor inputTensor, Texture2D inputTexture)
    {
        //Apply output tensor to a temp RenderTexture
        var tempRT = new RenderTexture(256, 256, 32);
        inputTensor.ToRenderTexture(tempRT);
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

    #endregion
}
