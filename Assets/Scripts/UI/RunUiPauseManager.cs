using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class RunUiPauseManager
{
    static readonly HashSet<string> PauseKeys = new HashSet<string>();
    static float previousTimeScale = 1f;

    // 버튼 클릭 시 EventSystem에 남는 "선택된 오브젝트"를 해제한다.
    // 해제하지 않으면 UI Submit 액션(기본 Enter 키 바인딩)이 마지막으로 클릭된
    // 버튼(예: 메뉴 버튼)을 다시 눌러, ESC/클릭이 아닌 Enter로도 패널이 열리는 문제가 생긴다.
    public static void ClearUiSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

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
