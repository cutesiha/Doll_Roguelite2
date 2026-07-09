using UnityEngine;

public static class ResolutionSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForceResolution()
    {
        Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
    }
}
