using System;
using DependencyAnalyzer.Editor.Controller.Issues;
using DependencyAnalyzer.Editor.Core;
using NUnit.Framework;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class IssueTargetResolverTests
    {
        [Test]
        public void FindIssueEntryTargetNodeId_PrefersIssueSourceEdge()
        {
            var graph = new DependencyGraphData();
            var source = CreateNode("script", "Assets/Foo.cs", "Foo.cs", "Script", DependencyNodeKind.Asset);
            var issue = new DependencyScanIssueData("Unity Console", "Assets/Foo.cs", "Assets/Foo.cs(8,13): error CS1003", DependencyScanIssueSeverity.Error);
            var issueNode = new DependencyNodeData(
                "issue:error:foo",
                default,
                "Assets/Foo.cs",
                "Foo.cs",
                "Script",
                "Script",
                0L,
                Array.Empty<string>(),
                "cs Script Icon",
                DependencyNodeKind.Asset,
                0,
                issue.Severity,
                issue.Message);

            graph.AddOrUpdateNode(source);
            graph.AddOrUpdateNode(issueNode);
            graph.AddEdge(new DependencyEdgeData(source.Id, issueNode.Id, "Error Issue", DependencyReferenceKind.Issue));

            Assert.AreEqual(source.Id, IssueTargetResolver.FindIssueEntryTargetNodeId(graph, issue));
        }

        [Test]
        public void IssuePanelEntryBuilder_IncludesMissingReferenceRows()
        {
            var graph = new DependencyGraphData();
            var missing = CreateNode("missing", "Scene/Object", "Missing Field", "Missing", DependencyNodeKind.MissingReference);
            graph.AddOrUpdateNode(missing);

            var entries = IssuePanelEntryBuilder.Build(graph);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(DependencyScanIssueSeverity.Warning, entries[0].Severity);
            Assert.AreEqual(missing.Id, entries[0].TargetNodeId);
        }

        private static DependencyNodeData CreateNode(string id, string path, string name, string type, DependencyNodeKind kind)
        {
            return new DependencyNodeData(
                id,
                default,
                path,
                name,
                type,
                type,
                0L,
                Array.Empty<string>(),
                "DefaultAsset Icon",
                kind);
        }
    }
}
