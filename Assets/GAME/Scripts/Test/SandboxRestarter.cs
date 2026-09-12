using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Reloads the active scene on a key press so a traversal tune can be retried
/// without leaving play mode.
///
/// Reads the keyboard directly rather than going through the intent pipeline:
/// this is a dev affordance, not a game action, and it has to keep working even
/// when the character is mid-knockdown or the input pipeline is wedged.
/// </summary>
public sealed class SandboxRestarter : MonoBehaviour
{
    [Tooltip("Key that reloads the scene.")]
    [SerializeField] private Key _restartKey = Key.R;

    [Tooltip("Strip this object out of release builds. The sandbox is a " +
        "development tool, so it should not ship.")]
    [SerializeField] private bool _editorAndDevelopmentBuildsOnly = true;

    private void Awake()
    {
        if (_editorAndDevelopmentBuildsOnly && !Debug.isDebugBuild)
            enabled = false;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (!keyboard[_restartKey].wasPressedThisFrame)
            return;

        Restart();
    }

    /// <summary>Reload the active scene from scratch.</summary>
    public void Restart()
    {
        Scene scene = SceneManager.GetActiveScene();

        // Anything the previous run left behind - a time scale from a hitstop,
        // a paused physics step - has to go before the reload, or the fresh
        // scene inherits it.
        Time.timeScale = 1f;

#if UNITY_EDITOR
        // Sandbox scenes are deliberately not in Build Settings, so buildIndex
        // is -1 and the runtime loaders cannot find them. The editor loader can.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            scene.path,
            new LoadSceneParameters(LoadSceneMode.Single));
#else
        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex);
            return;
        }

        Debug.LogWarning(
            $"{name}: '{scene.name}' is not in Build Settings, so it cannot " +
            "be reloaded at runtime.",
            this);
#endif
    }
}
