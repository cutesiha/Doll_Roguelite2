using McpUnity.Unity;
using UnityEditor;
using UnityEngine;

public static class McpUnityWindowMenuAlias
{
    [MenuItem("Window/MCP for Unity", false, 1)]
    public static void ShowMcpUnityWindow()
    {
        var window = EditorWindow.GetWindow<McpUnityEditorWindow>("MCP Unity");
        window.minSize = new Vector2(600f, 400f);
    }
}
