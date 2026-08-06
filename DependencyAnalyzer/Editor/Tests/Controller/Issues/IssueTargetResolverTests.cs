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
            var graph = new DependencyGraph();
            var source = CreateNode("script", "Assets/Foo.cs", "Foo.cs", "Script", DependencyNodeKind.Asset);
            var issue = new DependencyScanIssue("Unity Console", "Assets/Foo.cs", "Assets/Foo.cs(8,13): error CS1003", DependencyScanIssueSeverity.Error);
            var issueNode = new DependencyNode(
                "issue:error:foo",
                default,
                "Assets/Foo.cs",
                "Foo.cs",
                "Script",
                "Script",
                Array.Empty<string>(),
                "cs Script Icon",
                DependencyNodeKind.Asset,
                0,
                issue.Severity,
                issue.Message);

            graph.AddOrUpdateNode(source);
            graph.AddOrUpdateNode(issueNode);
            graph.AddEdge(new DependencyEdge(source.Id, issueNode.Id, "Error Issue", DependencyReferenceKind.Issue));

            Assert.AreEqual(source.Id, IssueTargetResolver.FindIssueEntryTargetNodeId(graph, issue));
        }

        private static DependencyNode CreateNode(string id, string path, string name, string type, DependencyNodeKind kind)
        {
            return new DependencyNode(
                id,
                default,
                path,
                name,
                type,
                type,
                Array.Empty<string>(),
                "DefaultAsset Icon",
                kind);
        }
    }
}
