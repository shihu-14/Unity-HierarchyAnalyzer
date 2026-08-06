using System;
using DependencyAnalyzer.Editor.Core;

namespace DependencyAnalyzer.Editor.Scanners.Issues
{
    internal static class IssueNodeLinker
    {
        public static void AddIssueNodes(DependencyGraph graph, DependencyNodeCache cache)
        {
            if (graph == null || graph.Issues.Count == 0)
            {
                return;
            }

            for (var i = 0; i < graph.Issues.Count; i++)
            {
                var issue = graph.Issues[i];
                if (!IsNodeLinkedIssueSeverity(issue.Severity))
                {
                    continue;
                }

                var sourceNodeId = FindIssueSourceNodeId(graph, issue.SubjectPath);
                if (string.IsNullOrEmpty(sourceNodeId))
                {
                    continue;
                }

                graph.TryGetNode(sourceNodeId, out var sourceNode);
                var issueNode = AssetScanner.CreateIssueNode(issue, cache, sourceNode);
                graph.AddOrUpdateNode(issueNode);

                graph.AddEdge(new DependencyEdge(
                    sourceNodeId,
                    issueNode.Id,
                    issue.Severity + " Issue",
                    DependencyReferenceKind.Issue));
            }
        }

        private static string FindIssueSourceNodeId(DependencyGraph graph, string subjectPath)
        {
            if (graph == null || string.IsNullOrEmpty(subjectPath))
            {
                return string.Empty;
            }

            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                if (string.Equals(node.Id, subjectPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.Path, subjectPath, StringComparison.OrdinalIgnoreCase))
                {
                    return node.Id;
                }
            }

            var bestNodeId = string.Empty;
            var bestLength = -1;
            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                if (string.IsNullOrEmpty(node.Path))
                {
                    continue;
                }

                var matches = subjectPath.IndexOf(node.Path, StringComparison.OrdinalIgnoreCase) >= 0
                    || node.Path.IndexOf(subjectPath, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matches || node.Path.Length <= bestLength)
                {
                    continue;
                }

                bestLength = node.Path.Length;
                bestNodeId = node.Id;
            }

            return bestNodeId;
        }

        private static bool IsNodeLinkedIssueSeverity(DependencyScanIssueSeverity severity)
        {
            return severity == DependencyScanIssueSeverity.Error
                || severity == DependencyScanIssueSeverity.Warning;
        }
    }
}
