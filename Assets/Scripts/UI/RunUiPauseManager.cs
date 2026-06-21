using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RunUiPauseManager
{
    static readonly HashSet<string> PauseKeys = new HashSet<string>();
    static float previousTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        ClearAll();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static void SetPaused(string key, bool paused)
    {
        if (string.IsNullOrEmpty(key))
            return;

        if (paused)
        {
            if (PauseKeys.Count == 0)
                previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;

            PauseKeys.Add(key);
            Time.timeScale = 0f;
            return;
        }

        PauseKeys.Remove(key);
        if (PauseKeys.Count == 0)
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
    }

    public static void ClearAll()
    {
        PauseKeys.Clear();
        previousTimeScale = 1f;
        Time.timeScale = 1f;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearAll();
    }
}
