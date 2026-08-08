using System;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.GraphView;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class DependencyGraphViewIssuePropagationTests
    {
        [Test]
        public void CollapseMissingTarget_CreatesTypeColoredGhostAtMissingOpacity()
        {
            var graph = new DependencyGraph();
            var source = CreateNode("source", "Source", DependencyNodeKind.SceneObject, "Object", "GameObject Icon");
            var missing = CreateNode("missing", "Missing Material", DependencyNodeKind.Asset, "Material", "Material Icon");
            missing.MarkAsMissingTarget(MissingTargetKind.BrokenReference);
            graph.AddOrUpdateNode(source);
            graph.AddOrUpdateNode(missing);
            graph.AddEdge(new DependencyEdge(
                source.Id,
                missing.Id,
                "m_Material",
                DependencyReferenceKind.Component,
                true));
            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 4);

            var missingView = FindView(graphView, missing.Id);
            Assert.IsNull(missingView.Q<Image>(className: "dependency-node-issue-marker"));
            Assert.IsTrue(missingView.ClassListContains("dependency-node--material"));

            graphView.CollapseNode(source.Id);

            var ghost = graphView.Q<VisualElement>(className: "dependency-node-ghost");
            Assert.NotNull(ghost);
            Assert.IsTrue(ghost.ClassListContains("dependency-node--material"));
            Assert.IsTrue(ghost.ClassListContains(DependencyNodeStyleResolver.MissingTargetClass));
            Assert.AreEqual(DependencyNodeStyleResolver.MissingTargetOpacity, ghost.style.opacity.value);
        }

        [Test]
        public void CollapseAndExpand_MovesWarningBetweenSourceAndNearestVisibleParent()
        {
            var graph = new DependencyGraph();
            var parent = CreateNode("parent", "Parent", DependencyNodeKind.SceneObject, "Object", "GameObject Icon");
            var source = CreateNode("source", "Source", DependencyNodeKind.Component, "Fixture", "cs Script Icon");
            var missing = CreateNode("missing", "Missing Material", DependencyNodeKind.Asset, "Material", "Material Icon");
            missing.MarkAsMissingTarget(MissingTargetKind.BrokenReference);
            graph.AddOrUpdateNode(parent);
            graph.AddOrUpdateNode(source);
            graph.AddOrUpdateNode(missing);
            graph.AddEdge(new DependencyEdge(
                parent.Id,
                source.Id,
                string.Empty,
                DependencyReferenceKind.Component));
            graph.AddEdge(new DependencyEdge(
                source.Id,
                missing.Id,
                "m_Material",
                DependencyReferenceKind.SerializedProperty,
                true));

            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 4);

            Assert.NotNull(FindView(graphView, source.Id)
                .Q<Image>(className: "dependency-node-issue-marker"));
            Assert.IsNull(FindView(graphView, parent.Id)
                .Q<Image>(className: "dependency-node-warning"));

            graphView.CollapseNode(parent.Id);

            Assert.IsNull(FindOptionalView(graphView, source.Id));
            Assert.NotNull(FindView(graphView, parent.Id)
                .Q<Image>(className: "dependency-node-warning"));

            graphView.ExpandNode(parent.Id);

            Assert.IsNull(FindView(graphView, parent.Id)
                .Q<Image>(className: "dependency-node-warning"));
            Assert.NotNull(FindView(graphView, source.Id)
                .Q<Image>(className: "dependency-node-issue-marker"));
        }

        private static DependencyNodeView FindView(DependencyGraphView graphView, string nodeId)
        {
            var nodeView = FindOptionalView(graphView, nodeId);
            Assert.NotNull(nodeView, "No visible node view for " + nodeId);
            return nodeView;
        }

        private static DependencyNodeView FindOptionalView(DependencyGraphView graphView, string nodeId)
        {
            return graphView.Query<DependencyNodeView>()
                .ToList()
                .SingleOrDefault(view => view.Data.Id == nodeId);
        }

        private static DependencyNode CreateNode(
            string id,
            string displayName,
            DependencyNodeKind kind,
            string typeName,
            string iconContentName)
        {
            return new DependencyNode(
                id,
                default,
                "Scene::" + displayName,
                displayName,
                typeName,
                typeName,
                Array.Empty<string>(),
                iconContentName,
                kind);
        }
    }
}
