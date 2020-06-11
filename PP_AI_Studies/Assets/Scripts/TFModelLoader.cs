using System.Collections;
using System.Collections.Generic;
using Unity.Barracuda;
using UnityEngine;
//using Tensorflow;
//using NumSharp;
//using System.IO;
//using static Tensorflow.Binding;


public class TFModelLoader : MonoBehaviour
{
    public NNModel modelAsset;
    private Model m_runtimeModel;
    public Texture2D test;
    public RenderTexture outputTexture;
    public Texture2D testTextureOutput;


    void Start()
    {
        m_runtimeModel = ModelLoader.Load(modelAsset);
        
        TestOnImage();
        TestAndWriteTexture();
    }

    void TestAndWriteTexture()
    {
        print(test.format);
        print(test.graphicsFormat);
        //Create the Barracuda worker
        var worker = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, m_runtimeModel);
        //Create the tensor from the Texture2D
        var tensor = new Tensor(test, channels: 3);
        //Normalize the tensor into [-1,1]
        var normalizedTensor = NormalizeTensor(tensor);
        //Execute the Worker on the normalized tensor
        worker.Execute(normalizedTensor);
        //Pull the output tensor from the worker
        var output = worker.PeekOutput();
        //Normalize the output tensor
        var outputNormalized = NormalizeTensorUp(output);
        //Assign result tensor to Texture 2D
        var ot = Tensor2Image(outputNormalized);
        //Apply output tensor to RenderTexture
        var rtOutput = new RenderTexture(256, 256, 32);
        outputNormalized.ToRenderTexture(rtOutput);
        RenderTexture.active = rtOutput;
        //Assign result texture to Texture2D
        //testTextureOutput.SetPixels(ot.GetPixels());
        testTextureOutput.ReadPixels(new Rect(0, 0, rtOutput.width, rtOutput.height), 0, 0);
        testTextureOutput.Apply();
        RenderTexture.active = null;
        Destroy(rtOutput);
    }

    void TestOnImage()
    {
        //Create the Barracuda worker
        var worker = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, m_runtimeModel);
        //Create the tensor from the Texture2D
        var tensor = new Tensor(test, channels: 3);
        //Normalize the tensor into [-1,1]
        var normalizedTensor = NormalizeTensor(tensor);
        //Execute the Worker on the normalized tensor
        worker.Execute(normalizedTensor);
        //Pull the output tensor from the worker
        var output = worker.PeekOutput();
        //Normalize the output tensor
        var outputNormalized = NormalizeTensorUp(output);
        //Apply the output tensor to the RenderTexture
        outputNormalized.ToRenderTexture(outputTexture);
    }

    Tensor NormalizeTensor(Tensor inputTensor)
    {
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
        var data = inputTensor.data.Download(inputTensor.shape);
        //var data = inputTensor.AsFloats();
        float[] normalized = new float[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            normalized[i] = Normalize(data[i], -1f, 1f, 0f, 1f);
        }

        return new Tensor(inputTensor.shape, normalized);
    }

    Tensor Image2Tensor(Texture2D inputImage)
    {
        var pixels = inputImage.GetPixels();
        float[] normalized = new float[256 * 256 * 3];
        for (int i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            var r = Normalize(pixel.r);
            var g = Normalize(pixel.g);
            var b = Normalize(pixel.b);
            normalized[i * 3 + 0] = r;
            normalized[i * 3 + 1] = g;
            normalized[i * 3 + 2] = b;
        }

        Tensor tensor = new Tensor(1, 256, 256, 3, normalized);
        return tensor;
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
