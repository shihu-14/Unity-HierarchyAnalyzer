using System;
using System.IO;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Scanners
{
    public static class AssetScanner
    {
        internal static DependencyNodeData CreateAssetNode(string assetPath, DependencyCache cache)
        {
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var type = mainAsset != null ? mainAsset.GetType() : AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var globalObjectId = mainAsset != null ? GlobalObjectId.GetGlobalObjectIdSlow(mainAsset) : default;
            var id = mainAsset != null ? globalObjectId.ToString() : "asset:" + assetPath;
            var labels = mainAsset != null ? AssetDatabase.GetLabels(mainAsset) : Array.Empty<string>();
            var typeName = type != null ? type.Name : Path.GetExtension(assetPath).TrimStart('.');
            var typeFullName = type != null ? type.FullName : typeName;
            var displayTypeName = GetDisplayTypeName(assetPath, typeName);
            var displayTypeFullName = GetDisplayTypeFullName(assetPath, typeFullName);
            var node = new DependencyNodeData(
                id,
                globalObjectId,
                assetPath,
                GetDisplayName(assetPath),
                displayTypeName,
                displayTypeFullName,
                GetFileSize(assetPath),
                labels,
                GetIconContentName(assetPath, type),
                DependencyNodeKind.Asset,
                mainAsset != null ? mainAsset.GetInstanceID() : 0);

            return cache.Store(node);
        }

        internal static DependencyNodeData CreateMissingNode(string id, string path, string displayName)
        {
            var node = new DependencyNodeData(
                id,
                default,
                path,
                displayName,
                "Missing",
                "Missing Reference",
                0L,
                Array.Empty<string>(),
                "console.warnicon.sml",
                DependencyNodeKind.MissingReference);
            node.MarkMissingReferences();
            return node;
        }

        private static string GetDisplayName(string assetPath)
        {
            return assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileName(assetPath)
                : Path.GetFileNameWithoutExtension(assetPath);
        }

        private static long GetFileSize(string assetPath)
        {
            var absolutePath = ToAbsolutePath(assetPath);
            if (!File.Exists(absolutePath))
            {
                return 0L;
            }

            return new FileInfo(absolutePath).Length;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        private static bool IsModelMeshPath(string assetPath)
        {
            var extension = Path.GetExtension(assetPath);
            return string.Equals(extension, ".fbx", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".obj", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".dae", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".blend", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".3ds", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".ma", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".mb", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetDisplayTypeName(string assetPath, string fallbackTypeName)
        {
            if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return "Prefab";
            }

            return IsModelMeshPath(assetPath) ? "Mesh" : fallbackTypeName;
        }

        private static string GetDisplayTypeFullName(string assetPath, string fallbackTypeName)
        {
            if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return "UnityEngine.GameObject";
            }

            return IsModelMeshPath(assetPath) ? "UnityEngine.Mesh" : fallbackTypeName;
        }

        private static string GetIconContentName(string assetPath, Type type)
        {
            if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return "Prefab Icon";
            }

            return IsModelMeshPath(assetPath) ? "Mesh Icon" : IconUtility.GetIconContentName(type);
        }
    }
}
