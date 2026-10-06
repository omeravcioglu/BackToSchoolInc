using UnityEditor;
using UnityEngine;

namespace SchoolSim.EditorTools
{
    /// <summary>
    /// Import settings for the game's audio (rendered by Tools/Music/render.py): the two music
    /// loops stream, so they are never decompressed into memory whole, and the short sound
    /// effects load decompressed so they play without a delay.
    /// </summary>
    sealed class AudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            if (assetPath.Contains("/Resources/Music/"))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.8f;
                importer.loadInBackground = true;
            }
            else if (assetPath.Contains("/Resources/Sfx/"))
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.9f;
            }
            else
            {
                return;
            }
            importer.defaultSampleSettings = settings;
        }
    }
}
