using System;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.GraphView;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class DependencyGraphViewDepthTests
    {
        [TestCase(1, 2)]
        [TestCase(3, 4)]
        [TestCase(5, 6)]
        public void Populate_FiniteDepthUsesExistingDepthSemantics(int depth, int expectedVisibleNodeCount)
        {
            var graphView = new DependencyGraphView();

            graphView.Populate(CreateRegularChain(6), depth);

            Assert.AreEqual(expectedVisibleNodeCount, VisibleNodeCount(graphView));
            Assert.AreEqual(depth, graphView.ExpansionDepth);
        }

        [Test]
        public void Populate_ZeroDepthShowsOnlyRoot()
        {
            var graphView = new DependencyGraphView();

            graphView.Populate(CreateRegularChain(2), 0);

            Assert.AreEqual(1, VisibleNodeCount(graphView));
            Assert.NotNull(FindVisibleNodeView(graphView, "depth:0"));
            Assert.IsNull(FindVisibleNodeView(graphView, "depth:1"));
            Assert.AreEqual(0, graphView.ExpansionDepth);
        }

        [Test]
        public void SetExpansionDepth_RerendersCurrentGraphWithoutAnotherScan()
        {
            var graph = CreateRegularChain(6);
            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 2);
            Assert.AreEqual(3, VisibleNodeCount(graphView));

            graphView.SetExpansionDepth(0);
            Assert.AreEqual(1, VisibleNodeCount(graphView));

            graphView.SetExpansionDepth(3);
            Assert.AreEqual(4, VisibleNodeCount(graphView));
        }

        [TestCase(-1, 0)]
        [TestCase(0, 0)]
        [TestCase(5, 5)]
        [TestCase(6, 6)]
        public void ClampExpansionDepth_UsesZeroThroughAllRange(int value, int expected)
        {
            Assert.AreEqual(expected, DependencyGraphView.ClampExpansionDepth(value));
        }

        [Test]
        public void SetExpansionDepth_ResetsRegularManualOverrides()
        {
            var graphView = new DependencyGraphView();
            graphView.Populate(CreateRegularChain(6), 1);
            graphView.ExpandNode("depth:1");
            Assert.AreEqual(3, VisibleNodeCount(graphView));

            graphView.SetExpansionDepth(3);
            Assert.AreEqual(4, VisibleNodeCount(graphView));

            graphView.CollapseNode("depth:0");
            Assert.AreEqual(1, VisibleNodeCount(graphView));

            graphView.SetExpansionDepth(4);

            Assert.AreEqual(5, VisibleNodeCount(graphView));
        }

        [Test]
        public void AllDepth_DisablesDepthHeavyLeafAndPrefabDefaultCollapse()
        {
            var graph = CreateSpecialCollapseGraph();
            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 5);

            Assert.IsNull(FindVisibleNodeView(graphView, "heavy-child"));
            Assert.IsNull(FindVisibleNodeView(graphView, "prefab-child"));
            Assert.IsNull(FindVisibleNodeView(graphView, "instance-child"));

            graphView.SetExpansionDepth(DependencyGraphView.AllExpansionDepthValue);

            Assert.NotNull(FindVisibleNodeView(graphView, "heavy-child"));
            Assert.NotNull(FindVisibleNodeView(graphView, "prefab-child"));
            Assert.NotNull(FindVisibleNodeView(graphView, "instance-child"));
        }

        [Test]
        public void AllDepth_PreservesCycleAndDuplicateSafety()
        {
            var graph = new DependencyGraph();
            var root = CreateNode("root", "Root", DependencyNodeKind.SceneObject, "GameObject", 101);
            var left = CreateNode("left", "Left");
            var right = CreateNode("right", "Right");
            var shared = CreateNode("shared", "Shared");
            var leaf = CreateNode("leaf", "Leaf");
            AddNodes(graph, root, left, right, shared, leaf);
            AddRegularEdge(graph, root, left);
            AddRegularEdge(graph, root, right);
            AddRegularEdge(graph, left, shared);
            AddRegularEdge(graph, right, shared);
            AddRegularEdge(graph, shared, leaf);
            AddRegularEdge(graph, leaf, root);

            var graphView = new DependencyGraphView();
            graphView.Populate(graph, DependencyGraphView.AllExpansionDepthValue);

            Assert.AreEqual(2, CountVisibleNodeViews(graphView, shared.Id));
            Assert.AreEqual(1, CountVisibleNodeViews(graphView, leaf.Id));
            Assert.LessOrEqual(VisibleNodeCount(graphView), 6);
        }

        [Test]
        public void AllDepth_AllowsManualCollapseAfterGlobalExpansion()
        {
            var graphView = new DependencyGraphView();
            graphView.Populate(CreateRegularChain(6), DependencyGraphView.AllExpansionDepthValue);
            Assert.AreEqual(7, VisibleNodeCount(graphView));

            graphView.CollapseNode("depth:2");

            Assert.AreEqual(3, VisibleNodeCount(graphView));
        }

        [Test]
        public void SetExpansionDepth_PreservesSerializedPropertyMenuExpansion()
        {
            var graph = new DependencyGraph();
            var root = CreateNode("root", "Root", DependencyNodeKind.SceneObject, "GameObject", 101);
            var regularChild = CreateNode("regular", "Regular Child");
            var menuChild = CreateNode("menu", "Inspector Reference", DependencyNodeKind.Asset, "Material");
            AddNodes(graph, root, regularChild, menuChild);
            AddRegularEdge(graph, root, regularChild);
            graph.AddEdge(new DependencyEdge(
                root.Id,
                menuChild.Id,
                "m_Material",
                DependencyReferenceKind.SerializedProperty));
            var graphView = new DependencyGraphView();
            graphView.Populate(graph, 2);
            Assert.IsNull(FindVisibleNodeView(graphView, menuChild.Id));

            graphView.SetExpansionDepth(DependencyGraphView.AllExpansionDepthValue);
            Assert.IsNull(FindVisibleNodeView(graphView, menuChild.Id));

            graphView.SetExpansionDepth(2);
            Assert.IsTrue(graphView.FocusNode(menuChild.Id, false));
            Assert.NotNull(FindVisibleNodeView(graphView, menuChild.Id));

            graphView.SetExpansionDepth(3);

            Assert.NotNull(FindVisibleNodeView(graphView, menuChild.Id));
        }

        private static DependencyGraph CreateRegularChain(int maximumDepth)
        {
            var graph = new DependencyGraph();
            var previous = CreateNode("depth:0", "Depth 0", DependencyNodeKind.SceneObject, "GameObject", 101);
            graph.AddOrUpdateNode(previous);
            for (var depth = 1; depth <= maximumDepth; depth++)
            {
                var current = CreateNode("depth:" + depth, "Depth " + depth);
                graph.AddOrUpdateNode(current);
                AddRegularEdge(graph, previous, current);
                previous = current;
            }

            return graph;
        }

        private static DependencyGraph CreateSpecialCollapseGraph()
        {
            var graph = new DependencyGraph();
            var root = CreateNode("root", "Root", DependencyNodeKind.SceneObject, "GameObject", 101);
            var heavy = CreateNode("heavy", "Texture", DependencyNodeKind.Asset, "Texture2D");
            var heavyChild = CreateNode("heavy-child", "Heavy Child");
            var prefab = CreateNode("prefab", "Prefab", DependencyNodeKind.Asset, "Prefab", path: "Assets/Fixture.prefab");
            var prefabChild = CreateNode("prefab-child", "Prefab Child");
            var instance = CreateNode("instance", "Prefab Instance", DependencyNodeKind.SceneObject, "GameObject", 102);
            var instanceChild = CreateNode("instance-child", "Instance Child");
            var sourcePrefab = CreateNode("source-prefab", "Source Prefab", DependencyNodeKind.Asset, "Prefab", path: "Assets/Source.prefab");
            AddNodes(graph, root, heavy, heavyChild, prefab, prefabChild, instance, instanceChild, sourcePrefab);
            AddRegularEdge(graph, root, heavy);
            AddRegularEdge(graph, heavy, heavyChild);
            AddRegularEdge(graph, root, prefab);
            AddRegularEdge(graph, prefab, prefabChild);
            AddRegularEdge(graph, root, instance);
            AddRegularEdge(graph, instance, instanceChild);
            graph.AddEdge(new DependencyEdge(
                instance.Id,
                sourcePrefab.Id,
                "Prefab Source",
                DependencyReferenceKind.PrefabInstance));
            return graph;
        }

        private static DependencyNode CreateNode(
            string id,
            string displayName,
            DependencyNodeKind kind = DependencyNodeKind.Component,
            string typeName = "FixtureComponent",
            int instanceId = 0,
            string path = null)
        {
            return new DependencyNode(
                id,
                default,
                path ?? "Scene::" + displayName,
                displayName,
                typeName,
                kind == DependencyNodeKind.SceneObject && typeName == "GameObject"
                    ? typeof(GameObject).FullName
                    : typeName,
                Array.Empty<string>(),
                "DefaultAsset Icon",
                kind,
                instanceId);
        }

        private static void AddNodes(DependencyGraph graph, params DependencyNode[] nodes)
        {
            for (var i = 0; i < nodes.Length; i++)
            {
                graph.AddOrUpdateNode(nodes[i]);
            }
        }

        private static void AddRegularEdge(DependencyGraph graph, DependencyNode source, DependencyNode target)
        {
            graph.AddEdge(new DependencyEdge(
                source.Id,
                target.Id,
                string.Empty,
                DependencyReferenceKind.Component));
        }

        private static int VisibleNodeCount(DependencyGraphView graphView)
        {
            return graphView.Query<DependencyNodeView>().ToList().Count;
        }

        private static int CountVisibleNodeViews(DependencyGraphView graphView, string nodeId)
        {
            return graphView.Query<DependencyNodeView>().ToList().Count(view => view.Data.Id == nodeId);
        }

        private static DependencyNodeView FindVisibleNodeView(DependencyGraphView graphView, string nodeId)
        {
            return graphView.Query<DependencyNodeView>().ToList().FirstOrDefault(view => view.Data.Id == nodeId);
        }
    }
}
