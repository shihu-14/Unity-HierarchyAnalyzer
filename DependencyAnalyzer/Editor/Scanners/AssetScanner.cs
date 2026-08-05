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
                0);

            return cache.Store(node);
        }

        internal static DependencyNodeData CreateAssetNode(UnityEngine.Object assetObject, DependencyCache cache)
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
            var node = new DependencyNodeData(
                id,
                globalObjectId,
                assetPath,
                GetDisplayName(assetPath, assetObject),
                displayTypeName,
                displayTypeFullName,
                GetFileSize(assetPath),
                labels,
                isMainAsset ? GetIconContentName(assetPath, type) : IconUtility.GetIconContentName(type),
                DependencyNodeKind.Asset,
                assetObject.GetInstanceID());

            return cache.Store(node);
        }

        internal static DependencyNodeData CreateMissingNode(
            string id,
            string path,
            string displayName,
            string typeName = "Missing",
            string namespaceQualifiedTypeName = "Missing Reference",
            string iconContentName = "console.warnicon.sml",
            DependencyNodeKind kind = DependencyNodeKind.MissingReference)
        {
            var node = new DependencyNodeData(
                id,
                default,
                path,
                displayName,
                typeName,
                namespaceQualifiedTypeName,
                0L,
                Array.Empty<string>(),
                iconContentName,
                kind,
                0,
                DependencyScanIssueSeverity.Warning,
                "Missing reference");
            node.MarkMissingReferences();
            return node;
        }

        internal static DependencyNodeData CreateIssueNode(
            DependencyScanIssueData issue,
            DependencyCache cache,
            DependencyNodeData sourceNode)
        {
            var severity = issue == null ? DependencyScanIssueSeverity.Warning : issue.Severity;
            var subjectPath = issue == null ? string.Empty : issue.SubjectPath;
            var message = issue == null ? string.Empty : issue.Message;
            var scannerName = issue == null ? string.Empty : issue.ScannerName;
            if (sourceNode != null)
            {
                var sourceIssueNode = new DependencyNodeData(
                    "issue:" + severity + ":" + GetStableHash(scannerName + "\n" + subjectPath + "\n" + message),
                    sourceNode.GlobalObjectId,
                    string.IsNullOrEmpty(subjectPath) ? sourceNode.Path : subjectPath,
                    sourceNode.DisplayName,
                    sourceNode.TypeName,
                    sourceNode.NamespaceQualifiedTypeName,
                    sourceNode.FileSizeBytes,
                    sourceNode.AssetLabels,
                    sourceNode.IconContentName,
                    sourceNode.Kind,
                    sourceNode.InstanceId,
                    severity,
                    message);
                return cache.Store(sourceIssueNode);
            }

            var node = new DependencyNodeData(
                "issue:" + severity + ":" + GetStableHash(scannerName + "\n" + subjectPath + "\n" + message),
                default,
                subjectPath,
                severity + ": " + (string.IsNullOrEmpty(message) ? "Issue" : message),
                severity + " Issue",
                "DependencyAnalyzer.Issue",
                0L,
                Array.Empty<string>(),
                GetIssueIconContentName(severity),
                DependencyNodeKind.Issue,
                0,
                severity,
                message);
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

            return IsModelMeshPath(assetPath) ? "Mesh Icon" : IconUtility.GetIconContentName(type);
        }

        private static string GetIssueIconContentName(DependencyScanIssueSeverity severity)
        {
            switch (severity)
            {
                case DependencyScanIssueSeverity.Error:
                    return "console.erroricon.sml";
                case DependencyScanIssueSeverity.Info:
                    return "console.infoicon.sml";
                default:
                    return "console.warnicon.sml";
            }
        }

        private static string GetStableHash(string value)
        {
            unchecked
            {
                const uint offset = 2166136261;
                const uint prime = 16777619;
                var hash = offset;
                if (!string.IsNullOrEmpty(value))
                {
                    for (var i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= prime;
                    }
                }

                return hash.ToString("x8");
            }
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
