using System;
using System.IO;
using DependencyAnalyzer.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Scanners
{
    internal static class AssetNodeFactory
    {
        internal static DependencyNode CreateAssetNode(string assetPath, DependencyNodeCache cache)
        {
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (mainAsset != null)
            {
                return CreateAssetNode(mainAsset, cache);
            }

            var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var globalObjectId = default(GlobalObjectId);
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            var id = string.IsNullOrEmpty(assetGuid) ? "asset:path:" + assetPath : "asset:" + assetGuid + ":0";
            var labels = Array.Empty<string>();
            var typeName = type != null ? type.Name : Path.GetExtension(assetPath).TrimStart('.');
            var typeFullName = type != null ? type.FullName : typeName;
            var displayTypeName = GetDisplayTypeName(assetPath, typeName);
            var displayTypeFullName = GetDisplayTypeFullName(assetPath, typeFullName);
            var node = new DependencyNode(
                id,
                globalObjectId,
                assetPath,
                GetDisplayName(assetPath),
                displayTypeName,
                displayTypeFullName,
                labels,
                GetIconContentName(assetPath, type),
                DependencyNodeKind.Asset,
                0);

            return cache.Store(node);
        }

        internal static DependencyNode CreateAssetNode(UnityEngine.Object assetObject, DependencyNodeCache cache)
        {
            if (assetObject == null)
            {
                throw new ArgumentNullException(nameof(assetObject));
            }

            var assetPath = AssetDatabase.GetAssetPath(assetObject);
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new ArgumentException("Object is not a project asset.", nameof(assetObject));
            }

            var type = assetObject.GetType();
            var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(assetObject);
            var id = BuildStableAssetId(assetObject, assetPath, globalObjectId);
            var labels = AssetDatabase.GetLabels(assetObject);
            var typeName = type.Name;
            var typeFullName = type.FullName;
            var isMainAsset = AssetDatabase.IsMainAsset(assetObject);
            var displayTypeName = isMainAsset ? GetDisplayTypeName(assetPath, typeName) : typeName;
            var displayTypeFullName = isMainAsset ? GetDisplayTypeFullName(assetPath, typeFullName) : typeFullName;
            var node = new DependencyNode(
                id,
                globalObjectId,
                assetPath,
                GetDisplayName(assetPath, assetObject),
                displayTypeName,
                displayTypeFullName,
                labels,
                isMainAsset ? GetIconContentName(assetPath, type) : UnityObjectIconNameResolver.GetIconContentName(type),
                DependencyNodeKind.Asset,
                assetObject.GetInstanceID());

            return cache.Store(node);
        }

        private static string GetDisplayName(string assetPath)
        {
            return assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileName(assetPath)
                : Path.GetFileNameWithoutExtension(assetPath);
        }

        private static string GetDisplayName(string assetPath, UnityEngine.Object assetObject)
        {
            if (AssetDatabase.IsMainAsset(assetObject)
                || assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(assetObject.name))
            {
                return GetDisplayName(assetPath);
            }

            return assetObject.name;
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
            if (assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return "Script";
            }

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

            return IsModelMeshPath(assetPath) ? "Mesh Icon" : UnityObjectIconNameResolver.GetIconContentName(type);
        }

        private static string BuildStableAssetId(
            UnityEngine.Object assetObject,
            string assetPath,
            GlobalObjectId globalObjectId)
        {
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(assetObject, out string guid, out long localId)
                && !string.IsNullOrEmpty(guid))
            {
                return "asset:" + guid + ":" + localId;
            }

            if (globalObjectId.identifierType != 0)
            {
                return "asset:" + globalObjectId;
            }

            return "asset:path:" + assetPath;
        }
    }
}
