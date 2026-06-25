using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Star : Body
{
    public Transform star;
    // Start is called before the first frame update
    protected override void Start()
    {
        base.Start();
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
        var radius = GetVisualRadiusInPixels(cam, FindObjectOfType<MainCamera>());
        float depth = Vector3.Dot(transform.position - cam.transform.position, cam.transform.forward);

        if (depth <= 0.0f) return;

        float worldSize = radius * 2 *
            (2.0f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)) *
            depth / Screen.height;

        star.localScale = Vector3.one * (float)(worldSize / (r * 1.9 * FindObjectOfType<MainCamera>().scale));
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
