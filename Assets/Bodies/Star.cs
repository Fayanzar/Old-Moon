using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Star : Body
{
    public Mesh starMesh;
    public Mesh starBillboard;

    public Material starMaterial;
    public Material billboardMaterial;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    protected override void Update()
    {
        base.Update();
    }

  public void LateUpdate()
    {
        UpdateBillboardSize();
    }

  void UpdateBillboardSize()
    {
        var cam = Camera.main;
        float distance = Vector3.Distance(cam.transform.position, transform.position);
        var meshFilter = GetComponent<MeshFilter>();
        var meshRenderer = GetComponent<MeshRenderer>();
        if (distance < transform.localScale.x * 20)
        {
            meshFilter.mesh = starMesh;
            meshRenderer.material = starMaterial;
            return;
        }
        meshFilter.mesh = starBillboard;
        meshRenderer.material = billboardMaterial;
        var radius = GetVisualRadiusInPixels(cam, FindObjectOfType<MainCamera>());

        float worldSize = radius * 4 *
            (2.0f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)) *
            distance / Screen.height;

        transform.localScale = Vector3.one * worldSize;
    }

    float GetVisualRadiusInPixels(Camera cam, MainCamera mainCamera)
    {
        // Get the screen-space size of the body based on its visual scale
        Vector3 center = cam.WorldToScreenPoint(transform.position);
        Vector3 edge   = cam.WorldToScreenPoint(
            transform.position + cam.transform.right *
            (float)(r * mainCamera.scale)); // * bodyScaleMultiplier

        return Vector2.Distance(center, edge);
    }
}
