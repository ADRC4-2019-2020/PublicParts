using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComponentGO : MonoBehaviour
{
    private Part _part;

    public Part GetPart()
    {
        return _part;
    }

    public void SetPart(Part part)
    {
        _part = part;
    }

    public void SelfDestroy()
    {
        Destroy(this.gameObject);
    }
}
