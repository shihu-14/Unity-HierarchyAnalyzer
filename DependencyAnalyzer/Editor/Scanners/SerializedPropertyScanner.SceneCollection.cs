using System;
using System.Collections.Generic;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DependencyAnalyzer.Editor.Scanners
{
    public sealed partial class SerializedPropertyScanner
    {
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
                        AddGameObjectHealthIssues(gameObject, gameObjectNode, graph);

                        var attachedComponents = gameObject.GetComponents<Component>();
                        for (var componentIndex = 0; componentIndex < attachedComponents.Length; componentIndex++)
                        {
                            var component = attachedComponents[componentIndex];
                            if (component == null)
                            {
                                var missingNode = AssetScanner.CreateMissingNode(
                                    "missing:component:" + gameObjectNode.Id + ":" + componentIndex,
                                    gameObjectNode.Path,
                                    "Missing MonoBehaviour",
                                    "Script",
                                    "UnityEngine.MonoBehaviour",
                                    "cs Script Icon",
                                    DependencyNodeKind.Component);
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

                            AddComponentHealthIssues(component, gameObjectNode, graph);

                            if (!ShouldVisualizeComponent(component))
                            {
                                AddHiddenComponentMaterialDependencies(component, gameObjectNode, graph, cache, settings);
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
