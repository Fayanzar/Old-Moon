using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrbitLine : MonoBehaviour
{
    public Solver solver;
    public MainCamera mainCamera;
    private Body[] bodies;
    private (GameObject, Body)[] lines;
    // Start is called before the first frame update
    void Start()
    {
        bodies = solver.bodies;
        lines = new (GameObject, Body)[bodies.Length];
        for (int i = 0; i < bodies.Length; i++) {
            lines[i].Item1 = new GameObject();
            lines[i].Item2 = bodies[i];
            var lineRenderer = lines[i].Item1.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.red;
            lineRenderer.endColor = Color.green;

            // Set the width
            lineRenderer.startWidth = 0.2f;
            lineRenderer.endWidth = 0.2f;

            // Set the number of vertices
            lineRenderer.positionCount = 5;
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        foreach (var line in lines)
        {
            if (line.Item1.GetComponent<LineRenderer>() == null) continue;
            var lineRenderer = line.Item1.GetComponent<LineRenderer>();
            Vector3[] positions = new Vector3[5];
            var centerPosition = mainCamera.centeredBody.position;
            for (int i = 0; i < 5; i++)
            {
                var p = line.Item2.position + line.Item2.velocity * 1000 * (i - 2);
                // if (i - 2 < 0)
                //     p -= line.Item2.acceleration * 1000 * 500 * (i - 2) * (i - 2);
                // else
                //     p += line.Item2.acceleration * 1000 * 500 * (i - 2) * (i - 2);
                var pointPosition = (Vector3)((p - centerPosition) * mainCamera.scale);
                positions[i] = pointPosition;
            }

            lineRenderer.SetPositions(positions);
        }
    }
}
