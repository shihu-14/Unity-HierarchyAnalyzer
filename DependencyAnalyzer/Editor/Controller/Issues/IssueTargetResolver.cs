using System;
using System.Linq;
using DependencyAnalyzer.Editor.Core;

namespace DependencyAnalyzer.Editor.Controller.Issues
{
    internal static class IssueTargetResolver
    {
        public static string FindIssueEntryTargetNodeId(DependencyGraphData graphData, DependencyScanIssueData issue)
        {
            if (graphData == null || issue == null)
            {
                return string.Empty;
            }

            var issueNode = FindIssueNode(graphData, issue);
            if (issueNode != null)
            {
                var sourceEdge = graphData.Edges.FirstOrDefault(edge =>
                    edge != null
                    && edge.ReferenceKind == DependencyReferenceKind.Issue
                    && string.Equals(edge.TargetNodeId, issueNode.Id, StringComparison.Ordinal));
                if (sourceEdge != null && !string.IsNullOrEmpty(sourceEdge.SourceNodeId))
                {
                    return sourceEdge.SourceNodeId;
                }
            }

            return FindIssueTargetNodeId(graphData, issue.SubjectPath);
        }

        private static DependencyNodeData FindIssueNode(DependencyGraphData graphData, DependencyScanIssueData issue)
        {
            if (graphData == null || issue == null)
            {
                return null;
            }

            return graphData.Nodes.FirstOrDefault(node =>
                node != null
                && node.HasIssue
                && node.IssueSeverity == issue.Severity
                && string.Equals(node.IssueMessage, issue.Message, StringComparison.Ordinal)
                && MatchesIssueSubject(node, issue.SubjectPath)
                && graphData.Edges.Any(edge =>
                    edge != null
                    && edge.ReferenceKind == DependencyReferenceKind.Issue
                    && string.Equals(edge.TargetNodeId, node.Id, StringComparison.Ordinal)));
        }

        private static bool MatchesIssueSubject(DependencyNodeData node, string subjectPath)
        {
            if (node == null || string.IsNullOrEmpty(subjectPath))
            {
                return false;
            }

            return string.Equals(node.Id, subjectPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.Path, subjectPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string FindIssueTargetNodeId(DependencyGraphData graphData, string subjectPath)
        {
            if (graphData == null || string.IsNullOrEmpty(subjectPath))
            {
                return string.Empty;
            }

            var exact = graphData.Nodes.FirstOrDefault(node =>
                string.Equals(node.Id, subjectPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.Path, subjectPath, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact.Id;
            }

            var containing = graphData.Nodes
                .Where(node => !string.IsNullOrEmpty(node.Path)
                    && (subjectPath.IndexOf(node.Path, StringComparison.OrdinalIgnoreCase) >= 0
                        || node.Path.IndexOf(subjectPath, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderByDescending(node => node.Path.Length)
                .FirstOrDefault();
            return containing == null ? string.Empty : containing.Id;
        }
    }
}
