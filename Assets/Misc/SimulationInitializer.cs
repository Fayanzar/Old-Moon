using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SimulationState { Uninitialized, Initializing, Ready }

public class SimulationInitializer : MonoBehaviour
{
    public static SimulationState State { get; private set; } = SimulationState.Uninitialized;
    public static event System.Action OnReady;

    [SerializeField] private Solver solver;
    [SerializeField] private GameObject[] trails;

    private IEnumerator Start()
    {
        State = SimulationState.Initializing;

        // Let Unity finish its first frame render (loading screen fade etc.)
        yield return new WaitForEndOfFrame();

        // Initialize simulation
        solver.InitialDerivatives();

        // Wait one more frame for GPU uploads (mesh, textures) to complete
        yield return new WaitForEndOfFrame();

        // Initialize trails, skybox, anything else that needs one frame to settle
        // foreach (var trail in trails)
        //     trail.RebuildMesh();

        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        State = SimulationState.Ready;
        OnReady?.Invoke();
    }
}
