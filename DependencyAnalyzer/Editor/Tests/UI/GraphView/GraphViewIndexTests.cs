using System;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.GraphView;
using NUnit.Framework;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class GraphViewIndexTests
    {
        [Test]
        public void Rebuild_UsesHierarchyRootsAndSortsThemByName()
        {
            var graph = new DependencyGraph();
            AddNode(graph, "zulu", "Zulu", 101);
            AddNode(graph, "alpha", "Alpha", 102);
            AddNode(graph, "child", "Child", 103);
            AddNode(graph, "asset", "Asset");
            graph.AddEdge(new DependencyEdge("alpha", "child", "Child", DependencyReferenceKind.Hierarchy));
            graph.AddEdge(new DependencyEdge("child", "zulu", "Reference", DependencyReferenceKind.SerializedProperty));
            var index = new GraphViewIndex();

            index.Rebuild(graph);

            CollectionAssert.AreEqual(new[] { "alpha", "zulu" }, index.RootNodes.Select(node => node.Id));
            Assert.AreEqual(1, index.MinimumRegularDepths["child"]);
            Assert.IsFalse(index.MinimumRegularDepths.ContainsKey("asset"));
        }

        [Test]
        public void Rebuild_PreservesRawReferencesButDeduplicatesAndOrdersTreeEdges()
        {
            var graph = new DependencyGraph();
            AddNode(graph, "root", "Root", 101);
            AddNode(graph, "alpha", "Alpha");
            AddNode(graph, "zulu", "Zulu");
            graph.AddEdge(new DependencyEdge("root", "zulu", "z", DependencyReferenceKind.SerializedProperty));
            graph.AddEdge(new DependencyEdge("root", "zulu", "b", DependencyReferenceKind.Component));
            graph.AddEdge(new DependencyEdge("root", "zulu", "a", DependencyReferenceKind.Component));
            graph.AddEdge(new DependencyEdge("root", "alpha", "a", DependencyReferenceKind.SerializedProperty));
            graph.AddEdge(new DependencyEdge("root", "absent", "gone", DependencyReferenceKind.Component));
            var index = new GraphViewIndex();

            index.Rebuild(graph);

            Assert.AreEqual(5, index.GetOutgoingEdges("root").Count);
            Assert.AreEqual(3, index.GetIncomingEdges("zulu").Count);
            CollectionAssert.AreEqual(new[] { "alpha", "zulu" }, index.GetTreeOutgoingEdges("root").Select(edge => edge.TargetNodeId));
            Assert.AreEqual("a", index.GetRegularTreeOutgoingEdges("root").Single().MemberName);
            Assert.AreEqual("alpha", index.GetMenuTreeOutgoingEdges("root").Single().TargetNodeId);
        }

        [Test]
        public void Rebuild_ComputesShortestRegularDepthWithoutFollowingMenuEdgesOrCycles()
        {
            var graph = new DependencyGraph();
            AddNode(graph, "root", "Root", 101);
            foreach (var id in new[] { "left", "middle", "shared", "menu" })
            {
                AddNode(graph, id, id);
            }

            graph.AddEdge(new DependencyEdge("root", "left", "Child", DependencyReferenceKind.Component));
            graph.AddEdge(new DependencyEdge("left", "middle", "Child", DependencyReferenceKind.Component));
            graph.AddEdge(new DependencyEdge("middle", "shared", "Child", DependencyReferenceKind.Component));
            graph.AddEdge(new DependencyEdge("root", "shared", "Child", DependencyReferenceKind.Component));
            graph.AddEdge(new DependencyEdge("shared", "root", "Cycle", DependencyReferenceKind.Component));
            graph.AddEdge(new DependencyEdge("root", "menu", "Reference", DependencyReferenceKind.SerializedProperty));
            var index = new GraphViewIndex();

            index.Rebuild(graph);

            Assert.AreEqual(0, index.MinimumRegularDepths["root"]);
            Assert.AreEqual(1, index.MinimumRegularDepths["shared"]);
            Assert.AreEqual(2, index.MinimumRegularDepths["middle"]);
            Assert.IsFalse(index.MinimumRegularDepths.ContainsKey("menu"));
        }

        [Test]
        public void Rebuild_DiscardsOldRootsEdgesDepthsAndInstanceMappings()
        {
            var graph = new DependencyGraph();
            AddNode(graph, "first", "First", 101);
            AddNode(graph, "duplicate", "Duplicate", 101);
            graph.AddEdge(new DependencyEdge("first", "duplicate", "Child", DependencyReferenceKind.Component));
            var index = new GraphViewIndex();
            index.Rebuild(graph);
            Assert.IsTrue(index.TryGetNodeByInstanceId(101, out var first));
            Assert.AreEqual("first", first.Id);

            var replacement = new DependencyGraph();
            AddNode(replacement, "new", "New", 202);
            index.Rebuild(replacement);

            Assert.IsFalse(index.TryGetNodeByInstanceId(101, out _));
            Assert.IsTrue(index.TryGetNodeByInstanceId(202, out _));
            CollectionAssert.AreEqual(new[] { "new" }, index.RootNodes.Select(node => node.Id));
            Assert.IsEmpty(index.GetOutgoingEdges("first"));
            Assert.IsEmpty(index.GetIncomingEdges("duplicate"));
            Assert.IsFalse(index.MinimumRegularDepths.ContainsKey("first"));
            index.Rebuild(null);
            Assert.IsEmpty(index.RootNodes);
            Assert.IsEmpty(index.MinimumRegularDepths);
            Assert.IsEmpty(index.GetTreeOutgoingEdges(null));
            Assert.IsFalse(index.TryGetNodeByInstanceId(202, out _));
        }

        private static void AddNode(DependencyGraph graph, string id, string name, int instanceId = 0)
        {
            graph.AddOrUpdateNode(new DependencyNode(id, default, id, name,
                instanceId == 0 ? "Material" : "GameObject",
                instanceId == 0 ? "UnityEngine.Material" : "UnityEngine.GameObject",
                Array.Empty<string>(), "GameObject Icon",
                instanceId == 0 ? DependencyNodeKind.Asset : DependencyNodeKind.SceneObject,
                instanceId));
        }
    }
}
