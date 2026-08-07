using System;
using System.Collections.Generic;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DependencyAnalyzer.Editor.Scanners
{
    public sealed partial class SerializedPropertyScanner
    {
        private static List<Component> CollectOpenSceneComponents(
            DependencyGraph graph,
            DependencyNodeCache cache,
            AnalyzerSettings settings)
        {
            var components = new List<Component>();
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene;
                try
                {
                    scene = SceneManager.GetSceneAt(sceneIndex);
                }
                catch (Exception exception)
                {
                    graph.AddIssue(new DependencyScanIssue(
                        ScannerName,
                        "Scene [" + sceneIndex + "]",
                        "Failed to access scene: " + exception.Message,
                        DependencyScanIssueSeverity.Warning));
                    continue;
                }

                if (!scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots;
                try
                {
                    roots = scene.GetRootGameObjects();
                }
                catch (Exception exception)
                {
                    graph.AddIssue(new DependencyScanIssue(
                        ScannerName,
                        scene.path,
                        "Failed to read scene roots: " + exception.Message,
                        DependencyScanIssueSeverity.Warning));
                    continue;
                }

                for (var i = 0; i < roots.Length; i++)
                {
                    try
                    {
                        foreach (var gameObject in Traverse(roots[i]))
                        {
                            try
                            {
                                CollectGameObject(
                                    gameObject,
                                    components,
                                    graph,
                                    cache,
                                    settings);
                            }
                            catch (Exception exception)
                            {
                                graph.AddIssue(new DependencyScanIssue(
                                    ScannerName,
                                    GetSafeObjectPath(gameObject),
                                    "Failed to collect object " + GetSafeObjectName(gameObject) + ": " + exception.Message,
                                    DependencyScanIssueSeverity.Warning));
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        graph.AddIssue(new DependencyScanIssue(
                            ScannerName,
                            scene.path,
                            "Failed to traverse root " + GetSafeObjectName(roots[i]) + ": " + exception.Message,
                            DependencyScanIssueSeverity.Warning));
                    }
                }
            }

            return components;
        }

        private static void CollectGameObject(
            GameObject gameObject,
            List<Component> components,
            DependencyGraph graph,
            DependencyNodeCache cache,
            AnalyzerSettings settings)
        {
            var gameObjectNode = CreateSceneObjectNode(gameObject, cache);
            graph.AddOrUpdateNode(gameObjectNode);
            AddHierarchyEdge(gameObject, gameObjectNode, graph, cache);
            AddPrefabSourceDependency(gameObject, gameObjectNode, graph, cache, settings);

            var attachedComponents = gameObject.GetComponents<Component>();
            for (var componentIndex = 0; componentIndex < attachedComponents.Length; componentIndex++)
            {
                var component = attachedComponents[componentIndex];
                try
                {
                    CollectComponent(
                        component,
                        componentIndex,
                        gameObjectNode,
                        components,
                        graph,
                        cache);
                }
                catch (Exception exception)
                {
                    graph.AddIssue(new DependencyScanIssue(
                        ScannerName,
                        gameObjectNode.Path,
                        "Failed to collect component " + GetSafeComponentTypeName(component) + ": " + exception.Message,
                        DependencyScanIssueSeverity.Warning));
                }
            }
        }

        private static void CollectComponent(
            Component component,
            int componentIndex,
            DependencyNode gameObjectNode,
            List<Component> components,
            DependencyGraph graph,
            DependencyNodeCache cache)
        {
            if (component == null)
            {
                var missingNode = MissingReferenceNodeFactory.CreateMissingNode(
                    "missing:component:" + gameObjectNode.Id + ":" + componentIndex,
                    gameObjectNode.Path,
                    "Missing MonoBehaviour",
                    "Script",
                    "UnityEngine.MonoBehaviour",
                    "cs Script Icon",
                    DependencyNodeKind.Component);
                gameObjectNode.MarkMissingReferences();
                graph.AddOrUpdateNode(missingNode);
                graph.AddEdge(new DependencyEdge(
                    gameObjectNode.Id,
                    missingNode.Id,
                    "Missing Component [" + componentIndex + "]",
                    DependencyReferenceKind.Component,
                    true));
                return;
            }

            components.Add(component);
            if (!ComponentScanPolicy.ShouldVisualizeComponent(component))
            {
                return;
            }

            var componentNode = CreateSceneObjectNode(component, cache);
            graph.AddOrUpdateNode(componentNode);
            graph.AddEdge(new DependencyEdge(
                gameObjectNode.Id,
                componentNode.Id,
                string.Empty,
                DependencyReferenceKind.Component));
        }

        private static void AddHierarchyEdge(
            GameObject gameObject,
            DependencyNode gameObjectNode,
            DependencyGraph graph,
            DependencyNodeCache cache)
        {
            var parent = gameObject.transform.parent;
            if (parent == null)
            {
                return;
            }

            var parentNode = CreateSceneObjectNode(parent.gameObject, cache);
            graph.AddOrUpdateNode(parentNode);
            graph.AddEdge(new DependencyEdge(
                parentNode.Id,
                gameObjectNode.Id,
                "Child",
                DependencyReferenceKind.Hierarchy));
        }

        private static void AddPrefabSourceDependency(
            GameObject gameObject,
            DependencyNode gameObjectNode,
            DependencyGraph graph,
            DependencyNodeCache cache,
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

            var prefabNode = AssetNodeFactory.CreateAssetNode(prefabAssetPath, cache);
            graph.AddOrUpdateNode(prefabNode);
            graph.AddEdge(new DependencyEdge(
                gameObjectNode.Id,
                prefabNode.Id,
                "Prefab Source",
                DependencyReferenceKind.PrefabInstance));
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

        private static string GetSafeObjectName(UnityEngine.Object unityObject)
        {
            try
            {
                return unityObject == null || string.IsNullOrEmpty(unityObject.name)
                    ? "(Unknown)"
                    : unityObject.name;
            }
            catch (Exception)
            {
                return "(Unknown)";
            }
        }
    }
}
