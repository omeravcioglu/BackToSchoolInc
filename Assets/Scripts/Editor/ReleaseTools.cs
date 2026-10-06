using System.Collections.Generic;
using System.IO;
using SchoolSim.Game;
using UnityEditor;
using UnityEngine;

namespace SchoolSim.EditorTools
{
    /// <summary>
    /// Release housekeeping: the game's icon, drawn from code in the notebook style (the school's
    /// crest with its acorn on a sky-blue tile), and the credits and font licences that ship next
    /// to the game. Menu: Tools/School Sim/Make App Icon.
    /// </summary>
    public static class ReleaseTools
    {
        const string IconPath = "Assets/Art/AppIcon.png";
        const string FontFolder = "Assets/UI/Resources/Fonts";

        [MenuItem("Tools/School Sim/Make App Icon")]
        public static void MakeAppIcon()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
            var texture = DrawIcon(1024).ToTexture(false, "AppIcon", readable: true);
            File.WriteAllBytes(IconPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
            AssetDatabase.SaveAssets();
            Debug.Log($"App icon written to {IconPath} and set as the default icon.");
        }

        /// <summary>The icon on a square canvas: a sky tile, the blue shield, and the acorn emblem.</summary>
        static InkCanvas DrawIcon(int size)
        {
            var canvas = new InkCanvas(size, size, 11) { Wobble = size / 512f };
            float s = size / 1024f;
            canvas.FillRect(36 * s, 30 * s, 988 * s, 982 * s, InkPalette.Ink, 190 * s);                  // ink shadow
            canvas.FillRect(24 * s, 48 * s, 976 * s, 1000 * s, InkIcons.Hex(0xA8D1F7), 190 * s);
            canvas.StrokeRect(24 * s, 48 * s, 976 * s, 1000 * s, InkPalette.Ink, 26 * s, 190 * s);

            // The crest from the HUD (CrestView): a 240x280 shield, scaled to fill most of the tile.
            var crest = new InkDrawing(240f, 280f)
                .Shape("M20 26Q120 -2 220 26V150C220 212 170 250 120 270C70 250 20 212 20 150Z", InkIcons.Hex(0x2F6FD0), InkPalette.Ink, 9f)
                .Line("M38 40Q120 16 202 40V150C202 202 162 234 120 252C78 234 38 202 38 150Z", new Color(1f, 1f, 1f, 0.9f), 5f);
            AddIcon(crest, "crest", new Vector2(120f, 134f), 9.2f);

            float scale = 2.65f * s;
            var offset = new Vector2((size - 240f * scale) / 2f, (size - 280f * scale) / 2f);
            crest.Rasterize(canvas, offset, scale);
            return canvas;
        }

        /// <summary>Places one of the HUD's icons (24x24 design) centred on a point of a drawing.</summary>
        static void AddIcon(InkDrawing d, string name, Vector2 centre, float scale)
        {
            var shapes = InkIcons.Get(name);
            if (shapes == null) return;
            var offset = centre - new Vector2(12f, 12f) * scale;
            foreach (var shape in shapes)
            {
                var segs = new List<InkPath.Seg>(shape.Segs.Count);
                foreach (var seg in shape.Segs) segs.Add(new InkPath.Seg(seg.Op, offset + seg.A * scale, offset + seg.B * scale, offset + seg.C * scale));
                var fill = shape.FillInk ? InkIcons.Ink : shape.Fill;
                var stroke = shape.StrokeInk ? InkIcons.Ink : shape.Stroke;
                d.AddPath(segs, fill, shape.Width > 0f ? stroke : Color.clear, Mathf.Max(0f, shape.Width) * scale);
            }
        }

        /// <summary>Copies the font licences and a short credits file into a build folder.</summary>
        public static void WriteLicenses(string buildFolder)
        {
            string folder = Path.Combine(buildFolder, "Licenses");
            Directory.CreateDirectory(folder);
            foreach (var file in Directory.GetFiles(FontFolder, "OFL-*.txt"))
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)), true);
            File.WriteAllText(Path.Combine(folder, "Credits.txt"),
                $"{PlayerSettings.productName} {PlayerSettings.bundleVersion}\n" +
                $"by {PlayerSettings.companyName}\n\n" +
                "Music and sounds: written for the game and played on the GeneralUser GS soundfont v2.0.3\n" +
                "by S. Christian Collins, whose licence allows its use in commercial music.\n\n" +
                "Fonts, used under the SIL Open Font License 1.1 (the licence texts are in this folder):\n" +
                "  Gochi Hand - Copyright (c) 2011, Juan Pablo del Peral\n" +
                "  Playpen Sans - Copyright 2023 The Playpen Sans Project Authors\n" +
                "  Caveat - Copyright 2014 The Caveat Project Authors\n\n" +
                "Made with Unity.\n");
        }
    }
}
