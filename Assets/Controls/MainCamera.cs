using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class MainCamera : MonoBehaviour
{
    public double scale = 1e-7;
    public double speed = 1;
    public Constants.TimeUnit timeUnit;
    public Body centeredBody;
    private double lastFixedTime;

    private double targetScale = 1e-7;
    private float scaleVelocity = 0f;
    // Start is called before the first frame update
    void OnValidate()
    {
        CenterBodies();
    }

    void FixedUpdate()
    {
        lastFixedTime = Time.timeAsDouble;
    }

    // Update is called once per frame
    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 movement = new Vector3(horizontal, 0, vertical);

        var rotation = transform.rotation;
        movement = rotation * movement;
        transform.position += movement * 0.01f;

        float yRotation = Input.GetAxis("Mouse X");
        float xRotation = Input.GetAxis("Mouse Y");
        float mouseScale = Input.GetAxis("Mouse ScrollWheel");
        targetScale *= 1 + mouseScale;
        scale = Mathf.SmoothDamp((float)scale, (float)targetScale, ref scaleVelocity, 0.15f);

        var yRotationQ = Quaternion.AngleAxis(yRotation, Vector3.up);
        var xRotationQ = Quaternion.AngleAxis(xRotation, Vector3.left);

        if (Input.GetButton("Fire2")) {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, transform.rotation * yRotationQ, 3);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, transform.rotation * xRotationQ, 3);
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, 0);
        }
    }

    public void CenterBody(Body body)
    {
        double alpha = (Time.timeAsDouble - lastFixedTime) / Time.fixedDeltaTime;
        alpha = Math.Clamp(alpha, 0.0, 1.0);

        var centerPosition = Vector3Double.Lerp(centeredBody.previousPosition, centeredBody.position, alpha);
        var bodyPosition = Vector3Double.Lerp(body.previousPosition, body.position, alpha);
        body.transform.position = (Vector3)((bodyPosition - centerPosition) * scale);
        body.transform.localScale = new Vector3(1, 1, 1) * (float)(body.r * 2 * scale);
    }

    public void CenterBodies()
    {
        var bodies = FindObjectsOfType<Body>();
        var centerPosition = centeredBody.position;
        for (int i = 0; i < bodies.Length; i++) {
            bodies[i].transform.position = (Vector3)((bodies[i].position - centerPosition) * scale);
            bodies[i].transform.localScale = new Vector3(1, 1, 1) * (float)(bodies[i].r * 2 * scale);
        }
    }
}
