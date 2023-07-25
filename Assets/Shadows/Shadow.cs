using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class Shadow : MonoBehaviour
{
    public GameObject[] occluders;

    void OnValidate()
    {
        Update();
    }

    void Update()
    {
        if (null != occluders)
        {
            var positions = occluders.Select( x => new Vector4(x.transform.position.x,
                                                               x.transform.position.y,
                                                               x.transform.position.z,
                                                               0                      )).ToList();
            var radii = occluders.Select( x => x.transform.localScale.x / 2f ).ToList();
            GetComponent<Renderer>().sharedMaterial.SetVectorArray("_SpherePositions", positions);
            GetComponent<Renderer>().sharedMaterial.SetFloatArray("_SphereRadii", radii);
            GetComponent<Renderer>().sharedMaterial.SetInt("_SphereNumber", occluders.Length);
        }
    }
}
