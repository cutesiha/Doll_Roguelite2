using UnityEngine;

public static class ResolutionSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForceResolution()
    {
        Screen.SetResolution(3440, 1440, FullScreenMode.Windowed);
    }
}
