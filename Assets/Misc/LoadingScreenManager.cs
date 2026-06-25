
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class LoadingScreenManager : MonoBehaviour
{
    [SerializeField] private Slider progressBar;
    [SerializeField] private string simulationSceneName = "Scenes/Moon";

    private void Start()
    {
        StartCoroutine(LoadSimulationAsync());
    }

    private IEnumerator LoadSimulationAsync()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(simulationSceneName);

        // Don't switch to the simulation scene automatically when done —
        // wait until we explicitly allow it, so we can show "Press any key
        // to continue" or finish a fade-out animation first
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            // AsyncOperation.progress goes 0→0.9 during load,
            // then jumps to 1.0 only after allowSceneActivation = true.
            // Remap 0→0.9 to 0→1 for display purposes.
            float displayProgress = Mathf.Clamp01(op.progress / 0.9f);
            progressBar.value = displayProgress;

            // Switch when load is complete (progress hits 0.9)
            if (op.progress >= 0.9f)
            {
                // Optionally wait for a minimum display time so the
                // loading screen doesn't flash by too fast on good hardware
                yield return new WaitForSeconds(0.5f);
                op.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}
