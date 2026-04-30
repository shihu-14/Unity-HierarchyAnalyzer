using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Scanners
{
    public sealed class AssetScanner : IDependencyScanner
    {
        private const string ScannerName = "Static Asset Scanner";

        private static readonly Regex ResourcesLoadPattern = new Regex(
            @"Resources\.(?:Load|LoadAsync)(?:<[^>\r\n]+>)?\s*\(\s*@?""([^""]+)""",
            RegexOptions.Compiled);

        public string Name => ScannerName;

        public async Task<DependencyGraphData> ScanAsync(
            AnalyzerSettings settings,
            DependencyCache cache,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken)
        {
            var graph = new DependencyGraphData();
            var assetPaths = CollectAssetPaths(settings);
            var resourcesLookup = BuildResourcesLookup(assetPaths);
            var batchSize = settings.ScanYieldBatchSize;

            for (var i = 0; i < assetPaths.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var assetPath = assetPaths[i];
                progress?.Report(new ScanProgress(Name, assetPath, i + 1, assetPaths.Count));

                if (IsSupportedStaticAsset(assetPath))
                {
                    ScanStaticAsset(assetPath, graph, cache, settings);
                    ScanSerializedAssetReferences(assetPath, graph, cache, settings);
                }

                if (assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    ScanResourcesLoadCalls(assetPath, graph, cache, resourcesLookup);
                }

                if (i % batchSize == 0)
                {
                    await Task.Yield();
                }
            }

            AppendAddressablesGroupDependencies(graph, cache, settings);
            graph.RecalculateReferenceCounts();
            return graph;
        }

        private static List<string> CollectAssetPaths(AnalyzerSettings settings)
        {
            var paths = new List<string>();
            var guids = AssetDatabase.FindAssets(string.Empty);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)
                    || AssetDatabase.IsValidFolder(path)
                    || settings.IsPathExcluded(path))
                {
                    continue;
                }

                paths.Add(path);
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        private static void ScanStaticAsset(
            string assetPath,
            DependencyGraphData graph,
            DependencyCache cache,
            AnalyzerSettings settings)
        {
            var sourceNode = CreateAssetNode(assetPath, cache);
            graph.AddOrUpdateNode(sourceNode);

            var dependencies = AssetDatabase.GetDependencies(assetPath, false);
            for (var i = 0; i < dependencies.Length; i++)
            {
                var dependencyPath = dependencies[i];
                if (string.Equals(assetPath, dependencyPath, StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrEmpty(dependencyPath)
                    || AssetDatabase.IsValidFolder(dependencyPath)
                    || settings.IsPathExcluded(dependencyPath))
                {
                    continue;
                }

                var targetNode = CreateAssetNode(dependencyPath, cache);
                graph.AddOrUpdateNode(targetNode);
                graph.AddEdge(new DependencyEdgeData(
                    sourceNode.Id,
                    targetNode.Id,
                    "AssetDatabase.GetDependencies",
                    DependencyReferenceKind.StaticAsset));
            }
        }

        private static void ScanSerializedAssetReferences(
            string assetPath,
            DependencyGraphData graph,
            DependencyCache cache,
            AnalyzerSettings settings)
        {
            var sourceNode = CreateAssetNode(assetPath, cache);
            graph.AddOrUpdateNode(sourceNode);

            var serializedAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (var assetIndex = 0; assetIndex < serializedAssets.Length; assetIndex++)
            {
                var serializedAsset = serializedAssets[assetIndex];
                if (!CanScanSerializedAsset(serializedAsset))
                {
                    continue;
                }

                SerializedObject serializedObject;
                try
                {
                    serializedObject = new SerializedObject(serializedAsset);
                }
                catch (Exception exception)
                {
                    graph.AddIssue(new DependencyScanIssueData(
                        ScannerName,
                        assetPath,
                        "Failed to inspect serialized asset " + serializedAsset.name + ": " + exception.Message,
                        DependencyScanIssueSeverity.Warning));
                    continue;
                }

                var property = serializedObject.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.propertyPath == "m_Script")
                    {
                        continue;
                    }

                    var referencedObject = property.objectReferenceValue;
                    if (referencedObject != null)
                    {
                        var targetPath = AssetDatabase.GetAssetPath(referencedObject);
                        if (string.IsNullOrEmpty(targetPath)
                            || string.Equals(targetPath, assetPath, StringComparison.OrdinalIgnoreCase)
                            || AssetDatabase.IsValidFolder(targetPath)
                            || settings.IsPathExcluded(targetPath))
                        {
                            continue;
                        }

                        var targetNode = CreateAssetNode(targetPath, cache);
                        graph.AddOrUpdateNode(targetNode);
                        graph.AddEdge(new DependencyEdgeData(
                            sourceNode.Id,
                            targetNode.Id,
                            serializedAsset.GetType().Name + "." + property.propertyPath,
                            DependencyReferenceKind.SerializedProperty));
                        continue;
                    }

                    if (property.objectReferenceInstanceIDValue != 0)
                    {
                        var missingNode = CreateMissingNode(
                            "missing:asset-property:" + sourceNode.Id + ":" + property.propertyPath + ":" + property.objectReferenceInstanceIDValue,
                            assetPath,
                            property.propertyPath);
                        sourceNode.MarkMissingReferences();
                        graph.AddOrUpdateNode(missingNode);
                        graph.AddEdge(new DependencyEdgeData(
                            sourceNode.Id,
                            missingNode.Id,
                            serializedAsset.GetType().Name + "." + property.propertyPath,
                            DependencyReferenceKind.SerializedProperty,
                            true));
                    }
                }
            }
        }

        private static void ScanResourcesLoadCalls(
            string scriptPath,
            DependencyGraphData graph,
            DependencyCache cache,
            Dictionary<string, List<string>> resourcesLookup)
        {
            var absolutePath = ToAbsolutePath(scriptPath);
            if (!File.Exists(absolutePath))
            {
                return;
            }

            var source = File.ReadAllText(absolutePath);
            var matches = ResourcesLoadPattern.Matches(source);
            if (matches.Count == 0)
            {
                return;
            }

            var sourceNode = CreateAssetNode(scriptPath, cache);
            graph.AddOrUpdateNode(sourceNode);
            foreach (Match match in matches)
            {
                var resourcesKey = NormalizeResourcesKey(match.Groups[1].Value);
                if (string.IsNullOrEmpty(resourcesKey))
                {
                    continue;
                }

                List<string> targetPaths;
                if (resourcesLookup.TryGetValue(resourcesKey, out targetPaths))
                {
                    for (var i = 0; i < targetPaths.Count; i++)
                    {
                        var targetNode = CreateAssetNode(targetPaths[i], cache);
                        graph.AddOrUpdateNode(targetNode);
                        graph.AddEdge(new DependencyEdgeData(
                            sourceNode.Id,
                            targetNode.Id,
                            "Resources.Load(\"" + resourcesKey + "\")",
                            DependencyReferenceKind.ResourcesLoad));
                    }
                }
                else
                {
                    var missingNode = CreateMissingNode(
                        "missing:resources:" + resourcesKey,
                        "Resources/" + resourcesKey,
                        "Missing Resources.Load target");
                    sourceNode.MarkMissingReferences();
                    graph.AddOrUpdateNode(missingNode);
                    graph.AddEdge(new DependencyEdgeData(
                        sourceNode.Id,
                        missingNode.Id,
                        "Resources.Load(\"" + resourcesKey + "\")",
                        DependencyReferenceKind.ResourcesLoad,
                        true));
                }
            }
        }

        private static Dictionary<string, List<string>> BuildResourcesLookup(List<string> assetPaths)
        {
            var lookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < assetPaths.Count; i++)
            {
                var path = assetPaths[i].Replace("\\", "/");
                var resourcesIndex = path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
                if (resourcesIndex < 0)
                {
                    continue;
                }

                var relativePath = path.Substring(resourcesIndex + "/Resources/".Length);
                var key = NormalizeResourcesKey(Path.ChangeExtension(relativePath, null));
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (!lookup.TryGetValue(key, out var paths))
                {
                    paths = new List<string>();
                    lookup.Add(key, paths);
                }

                paths.Add(path);
            }

            return lookup;
        }

        private static bool IsSupportedStaticAsset(string assetPath)
        {
            if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || IsModelMeshPath(assetPath))
            {
                return true;
            }

            var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (type == null)
            {
                return false;
            }

            return typeof(Material).IsAssignableFrom(type)
                || typeof(Texture).IsAssignableFrom(type)
                || typeof(AudioClip).IsAssignableFrom(type)
                || typeof(Mesh).IsAssignableFrom(type)
                || typeof(ScriptableObject).IsAssignableFrom(type);
        }

        internal static DependencyNodeData CreateAssetNode(string assetPath, DependencyCache cache)
        {
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var type = mainAsset != null ? mainAsset.GetType() : AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var globalObjectId = mainAsset != null ? GlobalObjectId.GetGlobalObjectIdSlow(mainAsset) : default;
            var id = mainAsset != null ? globalObjectId.ToString() : "asset:" + assetPath;
            var labels = mainAsset != null ? AssetDatabase.GetLabels(mainAsset) : Array.Empty<string>();
            var displayName = Path.GetFileNameWithoutExtension(assetPath);
            var typeName = type != null ? type.Name : Path.GetExtension(assetPath).TrimStart('.');
            var typeFullName = type != null ? type.FullName : typeName;
            var displayTypeName = GetDisplayTypeName(assetPath, typeName);
            var displayTypeFullName = GetDisplayTypeFullName(assetPath, typeFullName);
            var node = new DependencyNodeData(
                id,
                globalObjectId,
                assetPath,
                displayName,
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

        private static string NormalizeResourcesKey(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "/")
                .Trim()
                .TrimStart('/');
        }

        private static bool CanScanSerializedAsset(UnityEngine.Object serializedAsset)
        {
            if (serializedAsset == null || serializedAsset is DefaultAsset)
            {
                return false;
            }

            var type = serializedAsset.GetType();
            return type.Name != "MonoScript";
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
            return IsModelMeshPath(assetPath) ? "Mesh Icon" : IconUtility.GetIconContentName(type);
        }

        private static void AppendAddressablesGroupDependencies(
            DependencyGraphData graph,
            DependencyCache cache,
            AnalyzerSettings settings)
        {
            var settingsObject = GetAddressableSettingsObject(graph);
            if (settingsObject == null)
            {
                return;
            }

            var groupsProperty = settingsObject.GetType().GetProperty("groups", BindingFlags.Instance | BindingFlags.Public);
            var groups = groupsProperty?.GetValue(settingsObject) as IEnumerable;
            if (groups == null)
            {
                return;
            }

            foreach (var group in groups)
            {
                if (group == null)
                {
                    continue;
                }

                var groupName = ReadStringProperty(group, "Name");
                if (string.IsNullOrEmpty(groupName))
                {
                    groupName = ReadStringProperty(group, "name");
                }

                if (string.IsNullOrEmpty(groupName) || string.Equals(groupName, "Built In Data", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var entries = ReadEntries(group);
                if (entries.Count == 0)
                {
                    continue;
                }

                var groupNode = new DependencyNodeData(
                    "addressables-group:" + groupName,
                    default,
                    "Addressables/" + groupName,
                    groupName,
                    "AddressablesGroup",
                    "UnityEditor.AddressableAssets.Settings.AddressableAssetGroup",
                    0L,
                    Array.Empty<string>(),
                    "Folder Icon",
                    DependencyNodeKind.AddressablesGroup);
                graph.AddOrUpdateNode(groupNode);

                for (var i = 0; i < entries.Count; i++)
                {
                    var entryPath = entries[i];
                    if (settings.IsPathExcluded(entryPath))
                    {
                        continue;
                    }

                    var assetNode = CreateAssetNode(entryPath, cache);
                    graph.AddOrUpdateNode(assetNode);
                    graph.AddEdge(new DependencyEdgeData(
                        assetNode.Id,
                        groupNode.Id,
                        "Addressables Group: " + groupName,
                        DependencyReferenceKind.AddressablesGroup));
                    graph.AddEdge(new DependencyEdgeData(
                        groupNode.Id,
                        assetNode.Id,
                        "Packed With: " + groupName,
                        DependencyReferenceKind.AddressablesGroup));
                }
            }
        }

        private static object GetAddressableSettingsObject(DependencyGraphData graph)
        {
            var defaultObjectType = FindType("UnityEditor.AddressableAssets.Settings.AddressableAssetSettingsDefaultObject");
            if (defaultObjectType == null)
            {
                return null;
            }

            try
            {
                var settingsProperty = defaultObjectType.GetProperty("Settings", BindingFlags.Static | BindingFlags.Public);
                return settingsProperty?.GetValue(null);
            }
            catch (Exception exception)
            {
                graph.AddIssue(new DependencyScanIssueData(
                    "Addressables",
                    "Addressables",
                    "Failed to read Addressables settings: " + exception.Message,
                    DependencyScanIssueSeverity.Warning));
                return null;
            }
        }

        private static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static string ReadStringProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            return property?.GetValue(target) as string;
        }

        private static List<string> ReadEntries(object group)
        {
            var result = new List<string>();
            var entriesProperty = group.GetType().GetProperty("entries", BindingFlags.Instance | BindingFlags.Public);
            var entries = entriesProperty?.GetValue(group) as IEnumerable;
            if (entries == null)
            {
                return result;
            }

            foreach (var entry in entries)
            {
                var assetPath = ReadStringProperty(entry, "AssetPath");
                if (!string.IsNullOrEmpty(assetPath) && assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(assetPath);
                }
            }

            return result;
        }
    }
}
