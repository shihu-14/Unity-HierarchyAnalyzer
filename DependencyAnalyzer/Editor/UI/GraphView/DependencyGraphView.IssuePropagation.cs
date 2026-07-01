using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public sealed partial class DependencyGraphView
    {

        private void MarkNearestVisibleIssues()
        {
            for (var i = 0; i < renderNodes.Count; i++)
            {
                var renderNode = renderNodes[i];
                var outgoingEdges = GetTreeOutgoingEdges(renderNode.NodeId);
                for (var edgeIndex = 0; edgeIndex < outgoingEdges.Count; edgeIndex++)
                {
                    var edge = outgoingEdges[edgeIndex];
                    if (HasVisibleChildTarget(renderNode, edge.TargetNodeId))
                    {
                        continue;
                    }

                    if (!renderNode.Node.HasMissingReferences
                        && renderNode.Node.Kind != DependencyNodeKind.MissingReference
                        && SubtreeContainsMissingReference(edge.TargetNodeId, new HashSet<string> { renderNode.NodeId }))
                    {
                        renderNode.HasPropagatedMissingReference = true;
                    }

                    var issue = FindSubtreeIssue(edge.TargetNodeId, new HashSet<string> { renderNode.NodeId });
                    if (issue.HasIssue
                        && !ContainsVisibleDescendant(renderNode, issue.NodeId)
                        && !ContainsVisibleDescendant(renderNode, issue.SourceNodeId))
                    {
                        renderNode.SetPropagatedIssue(issue);
                    }
                }
            }
        }

        private static bool HasVisibleChildTarget(RenderNode renderNode, string targetNodeId)
        {
            for (var i = 0; i < renderNode.Children.Count; i++)
            {
                if (string.Equals(renderNode.Children[i].NodeId, targetNodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsVisibleDescendant(RenderNode renderNode, string nodeId)
        {
            if (renderNode == null || string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            var stack = new Stack<RenderNode>();
            for (var i = 0; i < renderNode.Children.Count; i++)
            {
                stack.Push(renderNode.Children[i]);
            }

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (string.Equals(current.NodeId, nodeId, StringComparison.Ordinal))
                {
                    return true;
                }

                for (var i = 0; i < current.Children.Count; i++)
                {
                    stack.Push(current.Children[i]);
                }
            }

            return false;
        }

        private bool SubtreeContainsMissingReference(string nodeId, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            if (missingReferenceSubtreeCache.TryGetValue(nodeId, out var cachedResult))
            {
                return cachedResult;
            }

            if (!visited.Add(nodeId) || !graph.TryGetNode(nodeId, out var node))
            {
                return false;
            }

            if (node.Kind == DependencyNodeKind.MissingReference || node.HasMissingReferences)
            {
                missingReferenceSubtreeCache[nodeId] = true;
                return true;
            }

            foreach (var edge in GetTreeOutgoingEdges(nodeId))
            {
                if (SubtreeContainsMissingReference(edge.TargetNodeId, visited))
                {
                    missingReferenceSubtreeCache[nodeId] = true;
                    return true;
                }
            }

            missingReferenceSubtreeCache[nodeId] = false;
            return false;
        }

        private SubtreeIssueState FindSubtreeIssue(string nodeId, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return SubtreeIssueState.None;
            }

            if (issueSubtreeCache.TryGetValue(nodeId, out var cachedResult))
            {
                return cachedResult;
            }

            if (!visited.Add(nodeId) || !graph.TryGetNode(nodeId, out var node))
            {
                return SubtreeIssueState.None;
            }

            var bestIssue = SubtreeIssueState.None;
            if (node.HasIssue
                && node.Kind != DependencyNodeKind.MissingReference
                && !node.HasMissingReferences
                && IsPropagatedIssueSeverity(node.IssueSeverity.Value))
            {
                bestIssue = new SubtreeIssueState(
                    true,
                    node.IssueSeverity.Value,
                    node.IssueMessage,
                    node.Id,
                    GetIssueSourceNodeId(node.Id));
            }

            foreach (var edge in GetTreeOutgoingEdges(nodeId))
            {
                var childIssue = FindSubtreeIssue(edge.TargetNodeId, visited);
                if (childIssue.HasIssue && (!bestIssue.HasIssue || IsMoreSevere(childIssue.Severity, bestIssue.Severity)))
                {
                    bestIssue = childIssue;
                }
            }

            issueSubtreeCache[nodeId] = bestIssue;
            return bestIssue;
        }

        private string GetIssueSourceNodeId(string issueNodeId)
        {
            if (string.IsNullOrEmpty(issueNodeId))
            {
                return string.Empty;
            }

            var incomingEdges = GetIncomingEdges(issueNodeId);
            for (var i = 0; i < incomingEdges.Count; i++)
            {
                var edge = incomingEdges[i];
                if (edge.ReferenceKind == DependencyReferenceKind.Issue)
                {
                    return edge.SourceNodeId;
                }
            }

            return string.Empty;
        }

        private static bool IsPropagatedIssueSeverity(DependencyScanIssueSeverity severity)
        {
            return severity == DependencyScanIssueSeverity.Error
                || severity == DependencyScanIssueSeverity.Warning;
        }

        private static bool IsMoreSevere(DependencyScanIssueSeverity candidate, DependencyScanIssueSeverity current)
        {
            return GetIssueSeverityRank(candidate) > GetIssueSeverityRank(current);
        }

        private static int GetIssueSeverityRank(DependencyScanIssueSeverity severity)
        {
            switch (severity)
            {
                case DependencyScanIssueSeverity.Error:
                    return 2;
                case DependencyScanIssueSeverity.Warning:
                    return 1;
                default:
                    return 0;
            }
        }
    }
}
