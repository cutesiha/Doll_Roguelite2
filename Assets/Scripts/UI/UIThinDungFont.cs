using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class UIThinDungFont
{
    const string ResourceFontPath = "Fonts/ThinDungGeunMo SDF";
    const string EditorFontPath = "Assets/TextMesh Pro/Fonts/ThinDungGeunMo SDF.asset";
    static TMP_FontAsset cachedFont;

    public static TMP_FontAsset Get(TMP_FontAsset fallback = null)
    {
        TMP_FontAsset koreanFont = GetKoreanFont();
        RegisterFallback(koreanFont);

        if (fallback != null)
        {
            RegisterFontFallback(fallback, koreanFont);
            return fallback;
        }

        return koreanFont != null ? koreanFont : TMP_Settings.defaultFontAsset;
    }

    static TMP_FontAsset GetKoreanFont()
    {
        if (cachedFont != null)
            return cachedFont;

        cachedFont = Resources.Load<TMP_FontAsset>(ResourceFontPath);
        if (cachedFont != null)
            return cachedFont;

        cachedFont = TMP_Settings.defaultFontAsset;
        if (cachedFont != null)
            return cachedFont;

#if UNITY_EDITOR
        cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(EditorFontPath);
        if (cachedFont != null)
            return cachedFont;
#endif

        return null;
    }

    static void RegisterFallback(TMP_FontAsset font)
    {
        if (font == null)
            return;

        var fallbacks = TMP_Settings.fallbackFontAssets;
        if (fallbacks != null && !fallbacks.Contains(font))
            fallbacks.Add(font);
    }

    static void RegisterFontFallback(TMP_FontAsset font, TMP_FontAsset fallback)
    {
        if (font == null || fallback == null || font == fallback)
            return;

        var table = font.fallbackFontAssetTable;
        if (table != null && !table.Contains(fallback))
            table.Add(fallback);
    }
}
