using UnityEditor;
using UnityEngine;

/// <summary>
/// MCPForUnity's Claude Code registration uses Application.dataPath (uppercase drive
/// letter on Windows, e.g. "C:\...") as the working directory when running `claude mcp add`.
/// VS Code's Claude Code extension resolves the same project path with a lowercase drive
/// letter ("c:\..."), so the two end up as different keys in ~/.claude.json and the
/// registration is invisible to the running session. Forcing MCPForUnity's
/// "Client Project Dir" override to the lowercase form fixes this at the source.
/// </summary>
public static class McpForceLowercaseProjectDir
{
    private const string PrefKey = "MCPForUnity.ClientProjectDir";

    [MenuItem("Tools/MCP For Unity/Fix Windows Drive-Letter Case (Claude Code)")]
    public static void Apply()
    {
        string projectDir = System.IO.Path.GetDirectoryName(Application.dataPath);
        string lowered = LowercaseDriveLetter(projectDir);

        EditorPrefs.SetString(PrefKey, lowered);

        Debug.Log($"[MCP Fix] Client Project Dir override set to: {lowered}\n" +
                   "Now open Window > MCP For Unity, select Claude Code, and click Configure " +
                   "(or Unregister then Configure if it already shows Configured) to re-register with the correct casing.");
    }

    private static string LowercaseDriveLetter(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length < 2 || path[1] != ':')
            return path;
        return char.ToLowerInvariant(path[0]) + path.Substring(1);
    }
}
