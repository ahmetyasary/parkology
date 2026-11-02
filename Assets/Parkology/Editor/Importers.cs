using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Parkology.Editor
{
    /// <summary>UI art becomes uncompressed-quality sprites with 9-slice borders; app icons stay plain textures.</summary>
    public sealed class ArtImport : AssetPostprocessor
    {
        static readonly Dictionary<string, Vector4> borders = new Dictionary<string, Vector4>
        {
            // left, bottom, right, top (pixels)
            { "btn-green", new Vector4(60, 60, 60, 60) }, { "btn-blue", new Vector4(60, 60, 60, 60) }, { "btn-red", new Vector4(60, 60, 60, 60) },
            { "btn-orange", new Vector4(60, 60, 60, 60) }, { "btn-yellow", new Vector4(60, 60, 60, 60) }, { "btn-purple", new Vector4(60, 60, 60, 60) },
            { "btn-grey", new Vector4(60, 60, 60, 60) },
            { "panel", new Vector4(64, 64, 64, 64) }, { "panel-header", new Vector4(64, 50, 64, 50) }, { "card", new Vector4(40, 40, 40, 40) },
            { "pill", new Vector4(50, 46, 50, 46) }, { "bar-track", new Vector4(36, 34, 36, 34) }, { "bar-fill", new Vector4(28, 27, 28, 27) },
            { "toggle-on", new Vector4(45, 44, 45, 44) }, { "toggle-off", new Vector4(45, 44, 45, 44) },
        };

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Art/")) return;
            var importer = (TextureImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (name == "app-icon")
            {
                importer.textureType = TextureImporterType.Default;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.maxTextureSize = name == "splash-bg" ? 2048 : 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.spriteBorder = borders.TryGetValue(name, out var b) ? b : Vector4.zero;
            importer.spritePixelsPerUnit = 100;
        }
    }

    /// <summary>Short effects stay PCM (sharp transients); the music loop streams as Vorbis.</summary>
    public sealed class AudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var a = (AudioImporter)assetImporter;
            var s = a.defaultSampleSettings;
            bool music = assetPath.EndsWith("music.wav");
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            s.quality = 0.6f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            a.defaultSampleSettings = s;
            a.forceToMono = true;
            a.loadInBackground = music;
        }
    }
}
