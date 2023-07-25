using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class Lights : MonoBehaviour
{
    public Light[] lights;
    // Update is called once per frame
    void Update()
    {
        var radii = lights.Select(x => x.transform.parent.transform.localScale.x / 2).ToArray();
        if (null != lights) {
            var positions = lights.Select( x => new Vector4(x.transform.position.x,
                                                            x.transform.position.y,
                                                            x.transform.position.z,
                                                            0                      )).ToList();
            var colors = lights.Select(x => new Vector4(x.color.r, x.color.g, x.color.b, 1)).ToList();
            GetComponent<Renderer>().sharedMaterial.SetVectorArray("_LightColors", colors);
            GetComponent<Renderer>().sharedMaterial.SetVectorArray("_LightPositions", positions);
            GetComponent<Renderer>().sharedMaterial.SetFloatArray("_LightRadii", radii);
            GetComponent<Renderer>().sharedMaterial.SetInt("_LightNumber", lights.Length);
        }
    }
}
