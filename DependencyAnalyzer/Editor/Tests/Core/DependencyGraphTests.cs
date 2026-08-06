using System;
using DependencyAnalyzer.Editor.Core;
using NUnit.Framework;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class DependencyGraphTests
    {
        [Test]
        public void AddOrUpdateNode_ReturnsExistingNodeAndMergesMissingState()
        {
            var graph = new DependencyGraph();
            var existing = CreateNode("node");
            var duplicate = CreateNode("node");
            duplicate.MarkMissingReferences();

            graph.AddOrUpdateNode(existing);
            var result = graph.AddOrUpdateNode(duplicate);

            Assert.AreSame(existing, result);
            Assert.AreEqual(1, graph.Nodes.Count);
            Assert.IsTrue(existing.HasMissingReferences);
        }

        [Test]
        public void AddEdge_DeduplicatesStableRelationship()
        {
            var graph = new DependencyGraph();
            graph.AddOrUpdateNode(CreateNode("source"));
            graph.AddOrUpdateNode(CreateNode("target"));
            var edge = new DependencyEdge(
                "source",
                "target",
                "reference",
                DependencyReferenceKind.SerializedProperty);

            Assert.IsTrue(graph.AddEdge(edge));
            Assert.IsFalse(graph.AddEdge(new DependencyEdge(
                "source",
                "target",
                "reference",
                DependencyReferenceKind.SerializedProperty)));
            Assert.AreEqual(1, graph.Edges.Count);
        }

        [Test]
        public void AddEdge_MarksSourceWhenTargetIsMissing()
        {
            var graph = new DependencyGraph();
            var source = CreateNode("source");
            graph.AddOrUpdateNode(source);
            graph.AddOrUpdateNode(CreateNode("missing"));

            graph.AddEdge(new DependencyEdge(
                source.Id,
                "missing",
                "reference",
                DependencyReferenceKind.SerializedProperty,
                true));

            Assert.IsTrue(source.HasMissingReferences);
        }

        [Test]
        public void RecalculateReferenceCounts_CountsDistinctConnectedNodes()
        {
            var graph = new DependencyGraph();
            var source = CreateNode("source");
            var firstTarget = CreateNode("first-target");
            var secondTarget = CreateNode("second-target");
            graph.AddOrUpdateNode(source);
            graph.AddOrUpdateNode(firstTarget);
            graph.AddOrUpdateNode(secondTarget);
            graph.AddEdge(new DependencyEdge(source.Id, firstTarget.Id, "first", DependencyReferenceKind.SerializedProperty));
            graph.AddEdge(new DependencyEdge(source.Id, firstTarget.Id, "second", DependencyReferenceKind.SerializedProperty));
            graph.AddEdge(new DependencyEdge(source.Id, secondTarget.Id, "third", DependencyReferenceKind.SerializedProperty));

            graph.RecalculateReferenceCounts();

            Assert.AreEqual(2, source.DependencyCount);
            Assert.AreEqual(1, firstTarget.UsedByCount);
            Assert.AreEqual(1, secondTarget.UsedByCount);
        }

        [Test]
        public void MergeFrom_DeduplicatesNodesAndEdgesAndPreservesIssues()
        {
            var destination = new DependencyGraph();
            var source = new DependencyGraph();
            source.AddOrUpdateNode(CreateNode("source"));
            source.AddOrUpdateNode(CreateNode("target"));
            source.AddEdge(new DependencyEdge("source", "target", "reference", DependencyReferenceKind.StaticAsset));
            source.AddIssue(new DependencyScanIssue(
                "TestScanner",
                "Assets/Test.asset",
                "Test issue",
                DependencyScanIssueSeverity.Warning));

            destination.MergeFrom(source);
            destination.MergeFrom(source);

            Assert.AreEqual(2, destination.Nodes.Count);
            Assert.AreEqual(1, destination.Edges.Count);
            Assert.AreEqual(2, destination.Issues.Count);
        }

        private static DependencyNode CreateNode(string id)
        {
            return new DependencyNode(
                id,
                default,
                "Assets/" + id + ".asset",
                id,
                "Object",
                "UnityEngine.Object",
                Array.Empty<string>(),
                "DefaultAsset Icon",
                DependencyNodeKind.Asset);
        }
    }
}
