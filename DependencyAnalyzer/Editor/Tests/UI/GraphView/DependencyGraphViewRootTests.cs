using System;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.GraphView;
using DependencyAnalyzer.Editor.UI.Issues;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class DependencyGraphViewRootTests
    {
        [Test]
        public void Populate_UsesOnlyHierarchyRootGameObjectsAndPreservesDependencies()
        {
            var graph = new DependencyGraph();
            var root = CreateSceneGameObject("root", "Root", 101);
            var child = CreateSceneGameObject("child", "Child", 102);
            var component = CreateNode(
                "component",
                "ReferenceFixtureComponent",
                "ReferenceFixtureComponent",
                typeof(MonoBehaviour).FullName,
                DependencyNodeKind.Component,
                103);
            var embeddedMaterial = CreateNode(
                "embedded-material",
                "Embedded Material",
                "Material",
                typeof(Material).FullName,
                DependencyNodeKind.SceneObject,
                104);
            var missingGameObject = CreateNode(
                "missing-game-object",
                "Missing GameObject",
                "GameObject",
                typeof(GameObject).FullName,
                DependencyNodeKind.SceneObject);
            missingGameObject.MarkAsMissingTarget(MissingTargetKind.BrokenReference);
            var texture = CreateNode(
                "texture",
                "Texture Asset",
                "Texture2D",
                typeof(Texture2D).FullName,
                DependencyNodeKind.Asset,
                105);
            var mesh = CreateNode(
                "mesh",
                "Mesh Asset",
                "Mesh",
                typeof(Mesh).FullName,
                DependencyNodeKind.Asset,
                106);
            var script = CreateNode(
                "script",
                "Script Asset",
                "MonoScript",
                "UnityEditor.MonoScript",
                DependencyNodeKind.Asset,
                107);

            graph.AddOrUpdateNode(root);
            graph.AddOrUpdateNode(child);
            graph.AddOrUpdateNode(component);
            graph.AddOrUpdateNode(embeddedMaterial);
            graph.AddOrUpdateNode(missingGameObject);
            graph.AddOrUpdateNode(texture);
            graph.AddOrUpdateNode(mesh);
            graph.AddOrUpdateNode(script);
            graph.AddEdge(new DependencyEdge(root.Id, child.Id, "Child", DependencyReferenceKind.Hierarchy));
            graph.AddEdge(new DependencyEdge(root.Id, component.Id, string.Empty, DependencyReferenceKind.Component));
            graph.AddEdge(new DependencyEdge(component.Id, embeddedMaterial.Id, "material", DependencyReferenceKind.SerializedProperty));
            graph.AddEdge(new DependencyEdge(component.Id, missingGameObject.Id, "target", DependencyReferenceKind.SerializedProperty, true));
            graph.AddEdge(new DependencyEdge(component.Id, texture.Id, "texture", DependencyReferenceKind.SerializedProperty));
            graph.AddEdge(new DependencyEdge(component.Id, mesh.Id, "mesh", DependencyReferenceKind.SerializedProperty));
            graph.AddEdge(new DependencyEdge(component.Id, script.Id, "script", DependencyReferenceKind.SerializedProperty));

            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 4);
            Assert.IsTrue(graphView.FocusNode(embeddedMaterial.Id, false));

            CollectionAssert.AreEqual(new[] { root.Id }, graphView.HierarchyRootNodes.Select(node => node.Id));
            Assert.AreEqual(1, CountViews(graphView, child.Id));
            Assert.AreEqual(1, CountViews(graphView, embeddedMaterial.Id));
            Assert.AreEqual(1, CountViews(graphView, missingGameObject.Id));
            Assert.AreEqual(1, CountViews(graphView, texture.Id));
            Assert.AreEqual(1, CountViews(graphView, mesh.Id));
            Assert.AreEqual(1, CountViews(graphView, script.Id));
            Assert.IsTrue(graph.Edges.Any(edge =>
                edge.SourceNodeId == component.Id
                && edge.TargetNodeId == embeddedMaterial.Id
                && edge.ReferenceKind == DependencyReferenceKind.SerializedProperty));
            Assert.IsTrue(graph.Edges.Any(edge =>
                edge.SourceNodeId == component.Id
                && edge.TargetNodeId == missingGameObject.Id
                && edge.PointsToMissingReference));
            Assert.AreEqual(1, ProjectIssuePanelBuilder.Build(graph).WarningCount);
        }

        [Test]
        public void Populate_WithoutHierarchyRootGameObject_ShowsEmptyStateWithoutPromotingDependencies()
        {
            var graph = new DependencyGraph();
            graph.AddOrUpdateNode(CreateNode(
                "material",
                "Material",
                "Material",
                typeof(Material).FullName,
                DependencyNodeKind.SceneObject,
                201));
            graph.AddOrUpdateNode(CreateNode(
                "asset",
                "Texture",
                "Texture2D",
                typeof(Texture2D).FullName,
                DependencyNodeKind.Asset,
                202));

            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 4);

            Assert.IsEmpty(graphView.HierarchyRootNodes);
            Assert.IsEmpty(graphView.Query<DependencyNodeView>().ToList());
            Assert.AreEqual(
                DisplayStyle.Flex,
                graphView.Q<Label>(className: "dependency-empty-state").style.display.value);
        }

        [Test]
        public void Populate_KeepsRootsFromMultipleLoadedSceneGraphs()
        {
            var graph = new DependencyGraph();
            var sceneARoot = CreateSceneGameObject("scene-a-root", "Scene A Root", 301, "SceneA");
            var sceneBRoot = CreateSceneGameObject("scene-b-root", "Scene B Root", 302, "SceneB");
            graph.AddOrUpdateNode(sceneARoot);
            graph.AddOrUpdateNode(sceneBRoot);

            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 4);

            CollectionAssert.AreEquivalent(
                new[] { sceneARoot.Id, sceneBRoot.Id },
                graphView.HierarchyRootNodes.Select(node => node.Id));
        }

        [Test]
        public void Populate_MergedIdentityProducesOneHierarchyRoot()
        {
            var graph = new DependencyGraph();
            graph.AddOrUpdateNode(CreateSceneGameObject("same-id", "Root", 401));
            graph.AddOrUpdateNode(CreateSceneGameObject("same-id", "Root Duplicate", 401));

            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 4);

            Assert.AreEqual(1, graph.Nodes.Count);
            Assert.AreEqual(1, graphView.HierarchyRootNodes.Count);
            Assert.AreEqual(1, CountViews(graphView, "same-id"));
        }

        private static DependencyNode CreateSceneGameObject(
            string id,
            string displayName,
            int instanceId,
            string sceneName = "Scene")
        {
            return CreateNode(
                id,
                displayName,
                "GameObject",
                typeof(GameObject).FullName,
                DependencyNodeKind.SceneObject,
                instanceId,
                sceneName + "::" + displayName);
        }

        private static DependencyNode CreateNode(
            string id,
            string displayName,
            string typeName,
            string namespaceQualifiedTypeName,
            DependencyNodeKind kind,
            int instanceId = 0,
            string path = null)
        {
            return new DependencyNode(
                id,
                default,
                path ?? "Scene::" + displayName,
                displayName,
                typeName,
                namespaceQualifiedTypeName,
                Array.Empty<string>(),
                "DefaultAsset Icon",
                kind,
                instanceId);
        }

        private static int CountViews(DependencyGraphView graphView, string nodeId)
        {
            return graphView.Query<DependencyNodeView>()
                .ToList()
                .Count(view => view.Data.Id == nodeId);
        }
    }
}
