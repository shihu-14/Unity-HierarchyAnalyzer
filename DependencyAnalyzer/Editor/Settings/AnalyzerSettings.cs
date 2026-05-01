using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Settings
{
    public sealed class AnalyzerSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/DependencyAnalyzer/Editor/Settings/AnalyzerSettings.asset";
        public const float DefaultZoomMin = 0.1f;
        public const float DefaultZoomMax = 10f;

        private static AnalyzerSettings runtimeDefaultSettings;

        [SerializeField]
        private List<string> excludedFolderPaths = new List<string>
        {
            "Assets/Library",
            "Assets/Temp",
            "Assets/Obj"
        };

        [SerializeField]
        private List<string> excludedExtensions = new List<string>
        {
            ".meta",
            ".csproj",
            ".sln"
        };

        [SerializeField]
        [Min(1)]
        private int scanYieldBatchSize = 64;

        [SerializeField]
        [Range(1, 4)]
        private int initialExpansionDepth = 2;

        [SerializeField]
        [Range(0.001f, 0.03f)]
        private float zoomStep = 0.004f;

        public IReadOnlyList<string> ExcludedFolderPaths => excludedFolderPaths;
        public IReadOnlyList<string> ExcludedExtensions => excludedExtensions;
        public int ScanYieldBatchSize => Mathf.Max(1, scanYieldBatchSize);
        public int InitialExpansionDepth => Mathf.Clamp(initialExpansionDepth, 1, 4);
        public float ZoomStep => Mathf.Clamp(zoomStep, 0.001f, 0.03f);

        public static AnalyzerSettings LoadOrCreateRuntimeSettings()
        {
            var settings = LoadSettingsAsset();
            if (settings != null)
            {
                return settings;
            }

            if (runtimeDefaultSettings == null)
            {
                runtimeDefaultSettings = CreateInstance<AnalyzerSettings>();
                runtimeDefaultSettings.hideFlags = HideFlags.HideAndDontSave;
            }

            return runtimeDefaultSettings;
        }

        public static AnalyzerSettings GetOrCreateSettingsAsset()
        {
            var settings = LoadSettingsAsset();
            if (settings != null)
            {
                return settings;
            }

            settings = CreateInstance<AnalyzerSettings>();
            EnsureParentFolderExists(AssetPath);
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        public static AnalyzerSettings GetOrCreateSettings()
        {
            return LoadOrCreateRuntimeSettings();
        }

        public bool IsPathExcluded(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return true;
            }

            var normalizedPath = NormalizeAssetPath(assetPath);
            for (var i = 0; i < excludedFolderPaths.Count; i++)
            {
                var excludedFolder = NormalizeAssetPath(excludedFolderPaths[i]);
                if (!string.IsNullOrEmpty(excludedFolder)
                    && normalizedPath.StartsWith(excludedFolder.TrimEnd('/') + "/", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var extension = Path.GetExtension(normalizedPath);
            for (var i = 0; i < excludedExtensions.Count; i++)
            {
                if (string.Equals(extension, excludedExtensions[i], System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return (assetPath ?? string.Empty).Replace("\\", "/").Trim();
        }

        public static AnalyzerSettings LoadSettingsAsset()
        {
            return AssetDatabase.LoadAssetAtPath<AnalyzerSettings>(AssetPath);
        }

        private static void EnsureParentFolderExists(string assetPath)
        {
            var folder = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
