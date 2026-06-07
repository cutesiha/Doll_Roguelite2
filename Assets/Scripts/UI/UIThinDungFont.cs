using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class UIThinDungFont
{
    const string FontPath = "Assets/TextMesh Pro/Fonts/ThinDungGeunMo SDF.asset";
    static TMP_FontAsset cachedFont;

    public static TMP_FontAsset Get(TMP_FontAsset fallback = null)
    {
        if (fallback != null)
            return fallback;

        if (cachedFont != null)
            return cachedFont;

#if UNITY_EDITOR
        cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (cachedFont != null)
            return cachedFont;
#endif

        return TMP_Settings.defaultFontAsset;
    }
}
