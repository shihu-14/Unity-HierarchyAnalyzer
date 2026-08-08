using System;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.GraphView;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class DependencyGraphViewSelectionTests
    {
        [Test]
        public void EditorSelection_PersistsAcrossRenderAndMovesToNewNode()
        {
            var graph = CreateGraph(out var alpha, out var beta);
            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 4);

            Assert.IsTrue(graphView.FocusNodeByInstanceId(alpha.InstanceId));
            Assert.AreEqual(alpha.Id, graphView.EditorSelectionNodeId);
            Assert.AreEqual(1, CountSelectionRings(graphView, alpha.Id));

            graphView.Populate(graph, 4);
            Assert.AreEqual(alpha.Id, graphView.EditorSelectionNodeId);
            Assert.AreEqual(1, CountSelectionRings(graphView, alpha.Id));

            Assert.IsTrue(graphView.FocusNodeByInstanceId(beta.InstanceId));
            Assert.AreEqual(beta.Id, graphView.EditorSelectionNodeId);
            Assert.AreEqual(0, CountSelectionRings(graphView, alpha.Id));
            Assert.AreEqual(1, CountSelectionRings(graphView, beta.Id));
        }

        [Test]
        public void EditorSelection_RepeatedFocusDoesNotDuplicatePulseSchedulerElement()
        {
            var graph = CreateGraph(out var alpha, out _);
            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 4);

            graphView.FocusNodeByInstanceId(alpha.InstanceId);
            graphView.FocusNodeByInstanceId(alpha.InstanceId);

            Assert.AreEqual(1, CountSelectionRings(graphView, alpha.Id));
        }

        [Test]
        public void EditorSelection_IsIndependentFromSearchCurrentHighlight()
        {
            var graph = CreateGraph(out var alpha, out _);
            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 4);

            graphView.FocusNodeByInstanceId(alpha.InstanceId);
            graphView.SetSearch("Alpha", false, false);
            var alphaView = FindView(graphView, alpha.Id);

            Assert.IsNotNull(alphaView.Q<VisualElement>(className: "dependency-node-search-ring"));
            Assert.IsNotNull(alphaView.Q<VisualElement>(className: DependencyGraphView.EditorSelectionHighlightClass));

            graphView.ClearEditorSelectionHighlight();

            Assert.IsNull(alphaView.Q<VisualElement>(className: DependencyGraphView.EditorSelectionHighlightClass));
            Assert.IsNotNull(alphaView.Q<VisualElement>(className: "dependency-node-search-ring"));
        }

        [Test]
        public void EditorSelection_BlankGraphClickClearsPersistentHighlight()
        {
            var graph = CreateGraph(out var alpha, out _);
            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 4);
            graphView.FocusNodeByInstanceId(alpha.InstanceId);

            graphView.HandleGraphBackgroundSelected();

            Assert.IsEmpty(graphView.EditorSelectionNodeId);
            Assert.AreEqual(0, graphView.Query<VisualElement>(
                className: DependencyGraphView.EditorSelectionHighlightClass).ToList().Count);
        }

        [Test]
        public void EditorSelection_GraphNodeClickClearsPersistentHighlight()
        {
            var graph = CreateGraph(out var alpha, out var beta);
            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 4);
            graphView.FocusNodeByInstanceId(alpha.InstanceId);
            graphView.HandleNodeSelected(beta);

            Assert.IsEmpty(graphView.EditorSelectionNodeId);
        }

        [Test]
        public void EditorSelection_UnknownInstanceClearsPersistentHighlight()
        {
            var graph = CreateGraph(out var alpha, out _);
            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 4);
            graphView.FocusNodeByInstanceId(alpha.InstanceId);

            Assert.IsFalse(graphView.FocusNodeByInstanceId(9999));
            Assert.IsEmpty(graphView.EditorSelectionNodeId);
        }

        private static DependencyGraph CreateGraph(out DependencyNode alpha, out DependencyNode beta)
        {
            var graph = new DependencyGraph();
            alpha = CreateNode("alpha", "Alpha", 101);
            beta = CreateNode("beta", "Beta", 202);
            graph.AddOrUpdateNode(alpha);
            graph.AddOrUpdateNode(beta);
            return graph;
        }

        private static DependencyNode CreateNode(string id, string displayName, int instanceId)
        {
            return new DependencyNode(
                id,
                default,
                "Scene::" + displayName,
                displayName,
                "GameObject",
                "UnityEngine.GameObject",
                Array.Empty<string>(),
                "GameObject Icon",
                DependencyNodeKind.SceneObject,
                instanceId);
        }

        private static DependencyNodeView FindView(DependencyGraphView graphView, string nodeId)
        {
            var view = graphView.Query<DependencyNodeView>()
                .ToList()
                .SingleOrDefault(candidate => candidate.Data.Id == nodeId);
            Assert.IsNotNull(view, "No visible node view for " + nodeId);
            return view;
        }

        private static int CountSelectionRings(DependencyGraphView graphView, string nodeId)
        {
            return FindView(graphView, nodeId)
                .Query<VisualElement>(className: DependencyGraphView.EditorSelectionHighlightClass)
                .ToList()
                .Count;
        }
    }
}
