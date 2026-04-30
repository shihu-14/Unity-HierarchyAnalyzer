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
        public string Name => "Serialized Property Scanner";

        public async Task<DependencyGraphData> ScanAsync(
            AnalyzerSettings settings,
            DependencyCache cache,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken)
        {
            var graph = new DependencyGraphData();
            var components = CollectOpenSceneComponents(graph, cache);
            var batchSize = settings.ScanYieldBatchSize;

            for (var i = 0; i < components.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                progress?.Report(new ScanProgress(Name, component.name, i + 1, components.Count));
                ScanComponent(component, graph, cache);

                if (i % batchSize == 0)
                {
                    await Task.Yield();
                }
            }

            graph.RecalculateReferenceCounts();
            return graph;
        }

        private static List<Component> CollectOpenSceneComponents(DependencyGraphData graph, DependencyCache cache)
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
                                    DependencyReferenceKind.SerializedProperty,
                                    true));
                                continue;
                            }

                            components.Add(component);
                            var componentNode = CreateSceneObjectNode(component, cache);
                            graph.AddOrUpdateNode(componentNode);
                            graph.AddEdge(new DependencyEdgeData(
                                gameObjectNode.Id,
                                componentNode.Id,
                                "Component",
                                DependencyReferenceKind.SerializedProperty));
                        }
                    }
                }
            }

            return components;
        }

        private static void ScanComponent(Component component, DependencyGraphData graph, DependencyCache cache)
        {
            var sourceNode = CreateSceneObjectNode(component, cache);
            graph.AddOrUpdateNode(sourceNode);

            SerializedObject serializedObject;
            try
            {
                serializedObject = new SerializedObject(component);
            }
            catch (Exception)
            {
                return;
            }

            var property = serializedObject.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.propertyPath == "m_Script")
                {
                    continue;
                }

                var referencedObject = property.objectReferenceValue;
                if (referencedObject != null)
                {
                    var targetNode = CreateObjectReferenceNode(referencedObject, cache);
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

        private static DependencyNodeData CreateObjectReferenceNode(UnityEngine.Object unityObject, DependencyCache cache)
        {
            var assetPath = AssetDatabase.GetAssetPath(unityObject);
            if (!string.IsNullOrEmpty(assetPath))
            {
                return AssetScanner.CreateAssetNode(assetPath, cache);
            }

            if (unityObject is GameObject || unityObject is Component)
            {
                return CreateSceneObjectNode(unityObject, cache);
            }

            var type = unityObject.GetType();
            var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(unityObject);
            var node = new DependencyNodeData(
                globalObjectId.ToString(),
                globalObjectId,
                unityObject.name,
                unityObject.name,
                type.Name,
                type.FullName,
                0L,
                Array.Empty<string>(),
                IconUtility.GetIconContentName(type),
                DependencyNodeKind.SceneObject);
            return cache.Store(node);
        }

        private static DependencyNodeData CreateSceneObjectNode(UnityEngine.Object unityObject, DependencyCache cache)
        {
            var type = unityObject.GetType();
            var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(unityObject);
            var path = GetObjectPath(unityObject);
            var node = new DependencyNodeData(
                globalObjectId.ToString(),
                globalObjectId,
                path,
                unityObject.name,
                type.Name,
                type.FullName,
                0L,
                Array.Empty<string>(),
                IconUtility.GetIconContentName(type),
                unityObject is Component ? DependencyNodeKind.Component : DependencyNodeKind.SceneObject);
            return cache.Store(node);
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
