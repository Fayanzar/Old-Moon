using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class SimulationInfo : MonoBehaviour
{
    public Solver solver;
    public MainCamera mainCamera;
    private TextMeshProUGUI textUI;
    // Start is called before the first frame update
    void Start()
    {
        textUI = GetComponent<TextMeshProUGUI>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        var simSpeed = solver.CurrentSimDt / Time.fixedDeltaTime;
        var targetSpeed = Constants.constDict[mainCamera.timeUnit] * mainCamera.speed;

        textUI.text = $@"Sim speed: {simSpeed}
Steps per tick: {solver.StepsPerTick}
Target speed: {targetSpeed * Time.fixedDeltaTime}
Dt: {solver.CurrentDt}";
    }
}
