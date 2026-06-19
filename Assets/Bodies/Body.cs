using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum BodyHighlightState { None, Hovered, Selected }

[ExecuteInEditMode]
public class Body : MonoBehaviour
{
    public double μ = 1;
    public double mass = 1;
    public double r = 1;
    public Vector3Double position;
    public Vector3Double velocity;
    public Vector3Double acceleration;
    public Vector3Double jerk;

    public Vector3Double previousPosition;
    public BodyHighlightState highlightState = BodyHighlightState.None;

    public RectTransform Selector
    {
        get; set;
    }

    public static Vector3Double GetGravitationalForce(Body body, Body[] bodies)
    {
        var force = new Vector3Double(0, 0, 0);
        for (int i = 0; i < bodies.Length; i++)
            if (bodies[i] != body) {
                Vector3Double rad = bodies[i].position - body.position;
                force += (Constants.G * body.mass * bodies[i].mass / rad.sqrMagnitude) * rad.normalized;
            }
        return force;
    }

    public void OnValidate()
    {
        // var centerPosition = FindObjectOfType<MainCamera>().centeredBody.position;
        if (μ != 0) mass = μ / Constants.G;
    }

    protected virtual void Update()
    {
        FindObjectOfType<MainCamera>().CenterBody(this);
        if (Selector != null) {
            Vector3 center = Camera.main.WorldToScreenPoint(transform.position);
            if (center.z < 0)
                Selector.gameObject.SetActive(false);
            else
            {
                Selector.gameObject.SetActive(true);
                Selector.position = new Vector3(center.x, center.y, 0);

                var selectorImage = Selector.GetComponent<Image>();
                Color newColor = highlightState switch
                {
                    BodyHighlightState.Hovered  => Color.white,
                    BodyHighlightState.Selected => Color.yellow,
                    _                           => Color.grey
                };
                selectorImage.color = newColor;
            }
        }
    }

    void OnDestroy()
    {
        if (Selector != null)
            Destroy(Selector);
    }
}
