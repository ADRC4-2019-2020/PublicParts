using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;

public class ConfigurablePartAgent : Agent
{
    private ConfigurablePart _part;

    public Part GetPart()
    {
        return _part;
    }

    public void SetPart(ConfigurablePart part)
    {
        _part = part;
    }

    public void SetVisibility(bool visible)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            var mRenderer = transform.GetChild(i).GetComponent<MeshRenderer>();
            mRenderer.enabled = visible;
        }
    }

    public void SelfDestroy()
    {
        Destroy(this.gameObject);
    }

    private void Update()
    {
        //THIS GUYS SHOULD BE MOVED TO HEURISTICS
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (_part.MoveInX(-1))
            {
                transform.position += new Vector3(-1, 0, 0) * _part.Grid.VoxelSize;
            }
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (_part.MoveInX(1))
            {
                transform.position += new Vector3(1, 0, 0) * _part.Grid.VoxelSize;
            }
        }
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (_part.MoveInZ(1))
            {
                transform.position += new Vector3(0, 0, 1) * _part.Grid.VoxelSize;
            }
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (_part.MoveInZ(-1))
            {
                transform.position += new Vector3(0, 0, -1) * _part.Grid.VoxelSize;
            }
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (_part.RotateComponent(1))
            {
                var currentRotation = transform.rotation;
                transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y + 90f, currentRotation.eulerAngles.z);
            }
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (_part.RotateComponent(-1))
            {
                var currentRotation = transform.rotation;
                transform.rotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y - 90f, currentRotation.eulerAngles.z);
            }
        }
    }
}
