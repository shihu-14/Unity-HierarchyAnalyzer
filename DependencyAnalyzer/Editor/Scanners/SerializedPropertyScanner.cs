using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DependencyAnalyzer.Editor.Scanners
{
    public sealed class SerializedPropertyScanner : IDependencyScanner
    {
        private const string ScannerName = "Serialized Property Scanner";

        public string Name => ScannerName;

        public async Task<DependencyGraphData> ScanAsync(
            AnalyzerSettings settings,
            DependencyCache cache,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken)
        {
            var graph = new DependencyGraphData();
            var components = CollectOpenSceneComponents(graph, cache, settings);
            var batchSize = settings.ScanYieldBatchSize;

            for (var i = 0; i < components.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                progress?.Report(new ScanProgress(Name, component.GetType().Name, i + 1, components.Count));
                ScanComponent(component, graph, cache, settings);

                if (i % batchSize == 0)
                {
                    await Task.Yield();
                }
            }

            graph.RecalculateReferenceCounts();
            return graph;
        }

        private static List<Component> CollectOpenSceneComponents(
            DependencyGraphData graph,
            DependencyCache cache,
            AnalyzerSettings settings)
        {
            var components = new List<Component>();
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                var roots = scene.GetRootGameObjects();
                for (var i = 0; i < roots.Length; i++)
                {
                    foreach (var gameObject in Traverse(roots[i]))
                    {
                        var gameObjectNode = CreateSceneObjectNode(gameObject, cache);
                        graph.AddOrUpdateNode(gameObjectNode);
                        AddHierarchyEdge(gameObject, gameObjectNode, graph, cache);
                        AddPrefabSourceDependency(gameObject, gameObjectNode, graph, cache, settings);

                        var attachedComponents = gameObject.GetComponents<Component>();
                        for (var componentIndex = 0; componentIndex < attachedComponents.Length; componentIndex++)
                        {
                            var component = attachedComponents[componentIndex];
                            if (component == null)
                            {
                                var missingNode = AssetScanner.CreateMissingNode(
                                    "missing:component:" + gameObjectNode.Id + ":" + componentIndex,
                                    gameObjectNode.Path,
                                    "Missing MonoBehaviour");
                                gameObjectNode.MarkMissingReferences();
                                graph.AddOrUpdateNode(missingNode);
                                graph.AddEdge(new DependencyEdgeData(
                                    gameObjectNode.Id,
                                    missingNode.Id,
                                    "Missing Component",
                                    DependencyReferenceKind.Hierarchy,
                                    true));
                                continue;
                            }

                            if (!ShouldVisualizeComponent(component))
                            {
                                continue;
                            }

                            components.Add(component);
                            var componentNode = CreateSceneObjectNode(component, cache);
                            graph.AddOrUpdateNode(componentNode);
                            graph.AddEdge(new DependencyEdgeData(
                                gameObjectNode.Id,
                                componentNode.Id,
                                string.Empty,
                                DependencyReferenceKind.Component));
                        }
                    }
                }
            }

            return components;
        }

        private static void ScanComponent(
            Component component,
            DependencyGraphData graph,
            DependencyCache cache,
            AnalyzerSettings settings)
        {
            var sourceNode = CreateSceneObjectNode(component, cache);
            graph.AddOrUpdateNode(sourceNode);

            SerializedObject serializedObject;
            try
            {
                serializedObject = new SerializedObject(component);
            }
            catch (Exception exception)
            {
                graph.AddIssue(new DependencyScanIssueData(
                    ScannerName,
                    sourceNode.Path,
                    "Failed to inspect component " + component.GetType().FullName + ": " + exception.Message,
                    DependencyScanIssueSeverity.Warning));
                return;
            }

            var property = serializedObject.GetIterator();
            while (property.NextVisible(true))
            {
                if (!ShouldScanInspectorObjectReference(component, property))
                {
                    continue;
                }

                var referencedObject = property.objectReferenceValue;
                if (referencedObject != null)
                {
                    if (referencedObject is MonoScript)
                    {
                        continue;
                    }

                    var targetNode = CreateObjectReferenceNode(referencedObject, cache, settings);
                    if (targetNode == null)
                    {
                        continue;
                    }

                    graph.AddOrUpdateNode(targetNode);
                    graph.AddEdge(new DependencyEdgeData(
                        sourceNode.Id,
                        targetNode.Id,
                        property.propertyPath,
                        DependencyReferenceKind.SerializedProperty));
                    continue;
                }

                if (property.objectReferenceInstanceIDValue != 0)
                {
                    var missingNode = AssetScanner.CreateMissingNode(
                        "missing:property:" + sourceNode.Id + ":" + property.propertyPath + ":" + property.objectReferenceInstanceIDValue,
                        sourceNode.Path,
                        property.propertyPath);
                    sourceNode.MarkMissingReferences();
                    graph.AddOrUpdateNode(missingNode);
                    graph.AddEdge(new DependencyEdgeData(
                        sourceNode.Id,
                        missingNode.Id,
                        property.propertyPath,
                        DependencyReferenceKind.SerializedProperty,
                        true));
                }
            }
        }

        private static bool ShouldScanInspectorObjectReference(Component component, SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                return false;
            }

            switch (property.propertyPath)
            {
                case "m_GameObject":
                case "m_CorrespondingSourceObject":
                case "m_PrefabInstance":
                case "m_PrefabAsset":
                case "m_PrefabParentObject":
                case "m_PrefabInternal":
                case "m_Father":
                case "m_Script":
                    return false;
                default:
                    return true;
            }
        }

        private static bool ShouldVisualizeComponent(Component component)
        {
            if (component == null)
            {
                return false;
            }

            if (IsDefaultTemplateComponent(component))
            {
                return false;
            }

            var monoBehaviour = component as MonoBehaviour;
            if (monoBehaviour != null)
            {
                var monoScript = MonoScript.FromMonoBehaviour(monoBehaviour);
                if (monoScript == null)
                {
                    return false;
                }

                var scriptPath = AssetDatabase.GetAssetPath(monoScript);
                return !string.IsNullOrEmpty(scriptPath)
                    && scriptPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private static bool IsDefaultTemplateComponent(Component component)
        {
            if (component is Transform || component is RectTransform)
            {
                return true;
            }

            if (component is MeshFilter || component is Renderer || component is Collider)
            {
                return true;
            }

            var gameObject = component.gameObject;
            var type = component.GetType();
            if (gameObject.GetComponent<Camera>() != null)
            {
                return type == typeof(Camera)
                    || type == typeof(AudioListener);
            }

            if (gameObject.GetComponent<Light>() != null)
            {
                return type == typeof(Light);
            }

            if (IsPrimitiveTemplateObject(gameObject))
            {
                return type == typeof(MeshFilter)
                    || type == typeof(MeshRenderer)
                    || type == typeof(BoxCollider)
                    || type == typeof(SphereCollider)
                    || type == typeof(CapsuleCollider)
                    || type == typeof(MeshCollider);
            }

            if (IsAudioSourceTemplateObject(gameObject))
            {
                return type == typeof(AudioSource);
            }

            return false;
        }

        private static bool IsPrimitiveTemplateObject(GameObject gameObject)
        {
            var meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null || gameObject.GetComponent<MeshRenderer>() == null)
            {
                return false;
            }

            var mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                return false;
            }

            var meshName = mesh.name;
            return string.Equals(meshName, "Cube", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Sphere", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Capsule", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Cylinder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Plane", StringComparison.OrdinalIgnoreCase)
                || string.Equals(meshName, "Quad", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAudioSourceTemplateObject(GameObject gameObject)
        {
            return gameObject.GetComponent<AudioSource>() != null
                && gameObject.GetComponents<Component>().Length == 2
                && gameObject.name.StartsWith("Audio Source", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddHierarchyEdge(
            GameObject gameObject,
            DependencyNodeData gameObjectNode,
            DependencyGraphData graph,
            DependencyCache cache)
        {
            var parent = gameObject.transform.parent;
            if (parent == null)
            {
                return;
            }

            var parentNode = CreateSceneObjectNode(parent.gameObject, cache);
            graph.AddOrUpdateNode(parentNode);
            graph.AddEdge(new DependencyEdgeData(
                parentNode.Id,
                gameObjectNode.Id,
                "Child",
                DependencyReferenceKind.Hierarchy));
        }

        private static void AddPrefabSourceDependency(
            GameObject gameObject,
            DependencyNodeData gameObjectNode,
            DependencyGraphData graph,
            DependencyCache cache,
            AnalyzerSettings settings)
        {
            if (PrefabUtility.GetNearestPrefabInstanceRoot(gameObject) != gameObject)
            {
                return;
            }

            var prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
            if (string.IsNullOrEmpty(prefabAssetPath)
                || AssetDatabase.IsValidFolder(prefabAssetPath)
                || settings.IsPathExcluded(prefabAssetPath))
            {
                return;
            }

            var prefabNode = AssetScanner.CreateAssetNode(prefabAssetPath, cache);
            graph.AddOrUpdateNode(prefabNode);
            graph.AddEdge(new DependencyEdgeData(
                gameObjectNode.Id,
                prefabNode.Id,
                "Prefab Source",
                DependencyReferenceKind.PrefabInstance));
        }

        private static DependencyNodeData CreateObjectReferenceNode(
            UnityEngine.Object unityObject,
            DependencyCache cache,
            AnalyzerSettings settings)
        {
            var assetPath = AssetDatabase.GetAssetPath(unityObject);
            if (unityObject is MonoScript)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(assetPath))
            {
                if (AssetDatabase.IsValidFolder(assetPath) || settings.IsPathExcluded(assetPath))
                {
                    return null;
                }

                return AssetScanner.CreateAssetNode(assetPath, cache);
            }

            if (unityObject is GameObject || unityObject is Component)
            {
                var component = unityObject as Component;
                if (component != null && !ShouldVisualizeComponent(component))
                {
                    return CreateSceneObjectNode(component.gameObject, cache);
                }

                return CreateSceneObjectNode(unityObject, cache);
            }

            var type = unityObject.GetType();
            var globalObjectId = GetGlobalObjectId(unityObject);
            var node = new DependencyNodeData(
                BuildObjectId("object", unityObject, globalObjectId),
                globalObjectId,
                unityObject.name,
                unityObject.name,
                type.Name,
                type.FullName,
                0L,
                Array.Empty<string>(),
                IconUtility.GetIconContentName(type),
                DependencyNodeKind.SceneObject,
                unityObject.GetInstanceID());
            return cache.Store(node);
        }

        private static DependencyNodeData CreateSceneObjectNode(UnityEngine.Object unityObject, DependencyCache cache)
        {
            var type = unityObject.GetType();
            var globalObjectId = GetGlobalObjectId(unityObject);
            var path = GetObjectPath(unityObject);
            var node = new DependencyNodeData(
                BuildObjectId("scene", unityObject, globalObjectId),
                globalObjectId,
                path,
                GetDisplayName(unityObject),
                GetDisplayTypeName(unityObject, type),
                type.FullName,
                0L,
                Array.Empty<string>(),
                GetIconContentName(unityObject, type),
                unityObject is Component ? DependencyNodeKind.Component : DependencyNodeKind.SceneObject,
                unityObject.GetInstanceID());
            return cache.Store(node);
        }

        private static string GetDisplayName(UnityEngine.Object unityObject)
        {
            if (unityObject is MonoBehaviour monoBehaviour)
            {
                return monoBehaviour.GetType().Name + " (Script)";
            }

            if (unityObject is Component component)
            {
                return component.GetType().Name;
            }

            return unityObject.name;
        }

        private static string GetDisplayTypeName(UnityEngine.Object unityObject, Type type)
        {
            if (unityObject is GameObject gameObject)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                {
                    return PrefabUtility.GetNearestPrefabInstanceRoot(gameObject) == gameObject
                        ? "Prefab Instance"
                        : "Prefab Child";
                }

                if (gameObject.GetComponent<Camera>() != null)
                {
                    return "Camera";
                }

                var light = gameObject.GetComponent<Light>();
                if (light != null)
                {
                    return light.type == LightType.Directional ? "Directional Light" : "Light";
                }

                return "Object";
            }

            return type.Name;
        }

        private static string GetIconContentName(UnityEngine.Object unityObject, Type type)
        {
            if (unityObject is GameObject gameObject)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                {
                    return "Prefab Icon";
                }

                if (gameObject.GetComponent<Camera>() != null)
                {
                    return "Camera Icon";
                }

                if (gameObject.GetComponent<Light>() != null)
                {
                    return "Light Icon";
                }
            }

            return IconUtility.GetIconContentName(type);
        }

        private static GlobalObjectId GetGlobalObjectId(UnityEngine.Object unityObject)
        {
            try
            {
                return GlobalObjectId.GetGlobalObjectIdSlow(unityObject);
            }
            catch (Exception)
            {
                return default;
            }
        }

        private static string BuildObjectId(string prefix, UnityEngine.Object unityObject, GlobalObjectId globalObjectId)
        {
            var globalObjectIdText = globalObjectId.ToString();
            if (!string.IsNullOrEmpty(globalObjectIdText))
            {
                return prefix + ":" + globalObjectIdText + ":" + unityObject.GetInstanceID();
            }

            return prefix + ":instance:" + unityObject.GetInstanceID();
        }

        private static string GetObjectPath(UnityEngine.Object unityObject)
        {
            if (unityObject is Component component)
            {
                return GetScenePath(component.gameObject.scene) + "::" + GetHierarchyPath(component.gameObject) + "/" + component.GetType().Name;
            }

            if (unityObject is GameObject gameObject)
            {
                return GetScenePath(gameObject.scene) + "::" + GetHierarchyPath(gameObject);
            }

            return unityObject.name;
        }

        private static string GetScenePath(Scene scene)
        {
            if (!string.IsNullOrEmpty(scene.path))
            {
                return scene.path;
            }

            return string.IsNullOrEmpty(scene.name) ? "Unsaved Scene" : scene.name;
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            var names = new Stack<string>();
            var current = gameObject.transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static IEnumerable<GameObject> Traverse(GameObject root)
        {
            yield return root;

            var transform = root.transform;
            for (var i = 0; i < transform.childCount; i++)
            {
                foreach (var child in Traverse(transform.GetChild(i).gameObject))
                {
                    yield return child;
                }
            }
        }
    }
}
