using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(MainCamera))]
[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(Solver))]
public class SelectBody : MonoBehaviour
{
    private Body[] bodies;
    private Camera cam;
    private MainCamera mainCamera;
    public float selectionRadiusPx = 20.0f;

    public GameObject selectionCircle;
    public GameObject label;
    public Body bodyUnderCursor;
    public Body selectedBody;
    public Canvas canvas;

    private bool isRefocusing = false;
    private Vector3 currentVelocity;
    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponent<Camera>();
        mainCamera = GetComponent<MainCamera>();
        bodies = GetComponent<Solver>().AllBodies;

        foreach (Body body in bodies)
        {
            var selector = Instantiate(selectionCircle).GetComponent<RectTransform>();
            selector.name = body.name + "_selector";
            selector.transform.SetParent(canvas.transform);
            selector.transform.SetAsFirstSibling();
            Vector3 center = cam.WorldToScreenPoint(body.transform.position);
            selector.position = new Vector3(center.x, center.y, 0);
            if (center.z < 0)
                selector.gameObject.SetActive(false);
            body.Selector = selector;

            var bodyLabel = Instantiate(label).GetComponent<RectTransform>();
            bodyLabel.name = body.name + "_label";
            bodyLabel.GetComponent<TextMeshProUGUI>().text = body.name;
            bodyLabel.transform.SetParent(canvas.transform);
            bodyLabel.transform.SetAsFirstSibling();
            bodyLabel.position = new Vector3(center.x, center.y + 50, 0);
            if (center.z < 0)
                bodyLabel.gameObject.SetActive(false);
            body.Label = bodyLabel;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.fKey.wasReleasedThisFrame && selectedBody != null && mainCamera.centeredBody != selectedBody && !isRefocusing)
        {
            isRefocusing = true;
            mainCamera.transform.position += mainCamera.centeredBody.transform.position - selectedBody.transform.position;
            mainCamera.centeredBody = selectedBody;
        }

        if (isRefocusing)
        {
            var targetR = mainCamera.centeredBody.transform.localScale.x * 1.5f;
            var targetPosition = transform.position.normalized * targetR;
            var targetRotation = Quaternion.LookRotation(-targetPosition);
            transform.SetPositionAndRotation(
                Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, 0.15f),
                Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 12));

            Vector3 angles   = Camera.main.transform.eulerAngles;
            mainCamera.Yaw   = angles.y;
            mainCamera.Pitch = angles.x;

            if ((transform.position - targetPosition).sqrMagnitude < 1f &&
                Quaternion.Angle(targetRotation, transform.rotation) < 1f)
                isRefocusing = false;
            else return;
        }

        var currentBodyUnderCursor = GetBodyUnderCursor();
        if (bodyUnderCursor != null && bodyUnderCursor.highlightState == BodyHighlightState.Hovered
            && currentBodyUnderCursor != bodyUnderCursor)
            bodyUnderCursor.highlightState = BodyHighlightState.None;

        if (currentBodyUnderCursor != null && currentBodyUnderCursor.highlightState == BodyHighlightState.None)
            currentBodyUnderCursor.highlightState = BodyHighlightState.Hovered;

        if (Mouse.current.leftButton.isPressed)
        {
            if (selectedBody != null && selectedBody != currentBodyUnderCursor)
                selectedBody.highlightState = BodyHighlightState.None;

            if (currentBodyUnderCursor != null)
                currentBodyUnderCursor.highlightState = BodyHighlightState.Selected;

            selectedBody = currentBodyUnderCursor;
        }
        bodyUnderCursor = currentBodyUnderCursor;
    }

    Body GetBodyUnderCursor()
    {
        var pointAction = InputSystem.actions.FindAction("Point");
        Vector2 mousePos = pointAction.ReadValue<Vector2>();

        Body closest = null;
        float closestDist = float.MaxValue;

        foreach (Body b in bodies)
        {
            // Project body position to screen space
            Vector3 screenPos = cam.WorldToScreenPoint(b.transform.position);

            // Behind camera
            if (screenPos.z < 0) continue;

            Vector2 screenPos2D = new(screenPos.x, screenPos.y);
            float dist = Vector2.Distance(mousePos, screenPos2D);

            // Use the larger of: projected visual radius, or minimum click radius
            float visualRadiusPx = GetVisualRadiusInPixels(b);
            float hitRadius = Mathf.Max(visualRadiusPx, selectionRadiusPx);

            if (dist < hitRadius && dist < closestDist)
            {
                closestDist = dist;
                closest = b;
            }
        }

        return closest; // null if nothing close
    }

    float GetVisualRadiusInPixels(Body b)
    {
        // Get the screen-space size of the body based on its visual scale
        Vector3 center = cam.WorldToScreenPoint(b.transform.position);
        Vector3 edge   = cam.WorldToScreenPoint(
            b.transform.position + cam.transform.right *
            (float)(b.r * mainCamera.scale)); // * bodyScaleMultiplier

        return Vector2.Distance(center, edge);
    }
}
