using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.UI;

public class PP_Academy_UI : MonoBehaviour
{
    private void FixedUpdate()
    {
        GetComponent<Text>().text = Academy.Instance.StepCount.ToString();
    }
}
