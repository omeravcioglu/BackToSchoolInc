using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.EditorTools
{
    /// <summary>
    /// Writes the notebook HUD's PanelSettings asset (theme, 1440x810 reference, Expand matching).
    /// GameHud loads it from Resources and only builds a temporary one when it's missing.
    /// Batch mode: -executeMethod SchoolSim.EditorTools.HudAssets.RebuildPanelSettings
    /// </summary>
    public static class HudAssets
    {
        const string SettingsPath = "Assets/UI/Resources/Hud/HudPanelSettings.asset";
        const string ThemePath = "Assets/UI/Resources/Hud/HudTheme.tss";

        [MenuItem("Tools/School Sim/Rebuild HUD Panel Settings")]
        public static void RebuildPanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(SettingsPath);
            bool create = settings == null;
            if (create) settings = ScriptableObject.CreateInstance<PanelSettings>();

            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1440, 810);
            settings.screenMatchMode = PanelScreenMatchMode.Expand;
            settings.clearColor = false;

            if (create) AssetDatabase.CreateAsset(settings, SettingsPath);
            else EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"HUD panel settings written to {SettingsPath} (theme: {(settings.themeStyleSheet != null ? "ok" : "MISSING")})");
        }
    }
}
