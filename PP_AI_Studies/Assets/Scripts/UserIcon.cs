using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UserIcon : MonoBehaviour
{
    private Tenant _tenant;
    private PPSpace _space;

    public void SetTenant(Tenant user)
    {
        _tenant = user;
    }

    public void SetSpace(PPSpace space, VoxelGrid grid)
    {
        _space = space;
        transform.localPosition = _space.GetCenter() + (new Vector3(0, 1.5f, 0) * grid.VoxelSize);
    }

    public void ReleaseSpace()
    {
        _space = null;
    }

    public void SelfDestroy()
    {
        Destroy(this.gameObject);
    }
}
