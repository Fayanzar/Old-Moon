using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StarSurface : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        GetComponent<Renderer>().sharedMaterial.SetFloat("_W_Angle", 0f);
        GetComponent<Renderer>().sharedMaterial.SetFloat("_W", 0f);
    }

    // Update is called once per frame
    void Update()
    {
        var wAngle = GetComponent<Renderer>().sharedMaterial.GetFloat("_W_Angle");
        var W = GetComponent<Renderer>().sharedMaterial.GetFloat("_W");
        if (wAngle > 2 * Mathf.PI) {
            wAngle -= 2 * Mathf.PI;
            W = 0;
        }
        if (wAngle > Mathf.PI)
            GetComponent<Renderer>().sharedMaterial.SetFloat("_W", W - Time.deltaTime * 0.002f);
        else
            GetComponent<Renderer>().sharedMaterial.SetFloat("_W", W + Time.deltaTime * 0.002f);
        GetComponent<Renderer>().sharedMaterial.SetFloat("_W_Angle", wAngle + Time.deltaTime * 0.01f);
        this.transform.Rotate(new Vector3(0, Time.deltaTime * 0.1f, 0), Space.Self);
    }
}
