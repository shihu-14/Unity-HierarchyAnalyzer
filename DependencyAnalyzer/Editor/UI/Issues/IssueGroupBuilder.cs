using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.GraphView;
using DependencyAnalyzer.Editor.UI.Icons;
using UnityEngine;

namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal static class IssueGroupBuilder
    {
        private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex CompilerLocationPrefixRegex = new Regex(
            @"^\s*(?:\(\d+(?:,\d+)?\))?\s*:\s*",
            RegexOptions.Compiled);
        private static readonly Regex CompilerCodeRegex = new Regex(@"\bCS\d{4}\b", RegexOptions.Compiled);

        public static int Count(
            DependencyGraph graphData,
            Func<DependencyGraph, DependencyScanIssue, string> resolveTargetNodeId)
        {
            return Summarize(Build(graphData, resolveTargetNodeId)).TotalCount;
        }

        public static List<IssueGroup> Build(
            DependencyGraph graphData,
            Func<DependencyGraph, DependencyScanIssue, string> resolveTargetNodeId)
        {
            var occurrences = BuildOccurrences(graphData, resolveTargetNodeId);
            return occurrences
                .GroupBy(occurrence => new GroupKey(
                    occurrence.Origin,
                    occurrence.Severity,
                    occurrence.GroupCategory,
                    occurrence.NormalizedMessage))
                .Select(group => CreateGroup(group.Key, group.ToList()))
                .OrderByDescending(group => group.Severity == DependencyScanIssueSeverity.Error)
                .ThenBy(group => group.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IssuePanelSummary Summarize(IReadOnlyList<IssueGroup> groups)
        {
            var consoleErrors = 0;
            var consoleWarnings = 0;
            var analyzerErrors = 0;
            var analyzerWarnings = 0;
            if (groups != null)
            {
                for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    var group = groups[groupIndex];
                    for (var occurrenceIndex = 0; occurrenceIndex < group.Occurrences.Count; occurrenceIndex++)
                    {
                        var occurrence = group.Occurrences[occurrenceIndex];
                        var isError = occurrence.Severity == DependencyScanIssueSeverity.Error;
                        if (occurrence.Origin == IssueOrigin.Console)
                        {
                            if (isError)
                            {
                                consoleErrors++;
                            }
                            else
                            {
                                consoleWarnings++;
                            }
                        }
                        else if (isError)
                        {
                            analyzerErrors++;
                        }
                        else
                        {
                            analyzerWarnings++;
                        }
                    }
                }
            }

            return new IssuePanelSummary(
                consoleErrors,
                consoleWarnings,
                analyzerErrors,
                analyzerWarnings);
        }

        public static string GetSeverityClass(DependencyScanIssueSeverity severity)
        {
            switch (severity)
            {
                case DependencyScanIssueSeverity.Error:
                    return "dependency-issue-row--error";
                case DependencyScanIssueSeverity.Warning:
                    return "dependency-issue-row--warning";
                default:
                    return "dependency-issue-row--info";
            }
        }

        private static List<IssueOccurrence> BuildOccurrences(
            DependencyGraph graphData,
            Func<DependencyGraph, DependencyScanIssue, string> resolveTargetNodeId)
        {
            var occurrences = new List<IssueOccurrence>();
            if (graphData == null)
            {
                return occurrences;
            }

            AddMissingReferenceOccurrences(graphData, occurrences);
            AddDiagnosticOccurrences(graphData, resolveTargetNodeId, occurrences);
            return occurrences;
        }

        private static void AddMissingReferenceOccurrences(
            DependencyGraph graphData,
            ICollection<IssueOccurrence> occurrences)
        {
            var missingTargetIds = new HashSet<string>(
                graphData.Edges
                    .Where(edge => edge != null && edge.PointsToMissingReference)
                    .Select(edge => edge.TargetNodeId),
                StringComparer.Ordinal);
            var missingNodes = graphData.Nodes
                .Where(node => node.Kind == DependencyNodeKind.MissingReference || missingTargetIds.Contains(node.Id))
                .OrderBy(node => node.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase);

            foreach (var missingNode in missingNodes)
            {
                var sourceEdge = graphData.Edges.FirstOrDefault(edge =>
                    edge != null
                    && edge.PointsToMissingReference
                    && string.Equals(edge.TargetNodeId, missingNode.Id, StringComparison.Ordinal));
                DependencyNode sourceNode = null;
                if (sourceEdge != null)
                {
                    graphData.TryGetNode(sourceEdge.SourceNodeId, out sourceNode);
                }

                var relatedNode = sourceNode ?? missingNode;
                var missingType = GetMissingReferenceType(missingNode);
                var category = "Missing Reference/" + missingType;
                var locationLabel = sourceEdge == null || string.IsNullOrEmpty(sourceEdge.MemberName)
                    ? missingNode.DisplayName
                    : sourceEdge.MemberName;
                occurrences.Add(new IssueOccurrence(
                    category,
                    category,
                    GetMissingReferenceTitle(missingType),
                    "Missing serialized reference",
                    DependencyScanIssueSeverity.Warning,
                    relatedNode.Id,
                    DependencyIconProvider.GetIcon(relatedNode),
                    DependencyNodeStyleResolver.GetNodeAccentColor(relatedNode),
                    DependencyIconProvider.GetIcon(missingNode),
                    IssueOrigin.Analyzer,
                    BuildLocation(relatedNode, locationLabel, null),
                    1));
            }
        }

        private static void AddDiagnosticOccurrences(
            DependencyGraph graphData,
            Func<DependencyGraph, DependencyScanIssue, string> resolveTargetNodeId,
            ICollection<IssueOccurrence> occurrences)
        {
            foreach (var issue in graphData.Issues)
            {
                if (!IsDisplayedSeverity(issue.Severity))
                {
                    continue;
                }

                var targetNodeId = resolveTargetNodeId == null
                    ? string.Empty
                    : resolveTargetNodeId(graphData, issue);
                DependencyNode targetNode = null;
                var hasTargetNode = !string.IsNullOrEmpty(targetNodeId)
                    && graphData.TryGetNode(targetNodeId, out targetNode);
                var origin = GetOrigin(issue);
                var normalizedMessage = NormalizeMessage(issue);
                var category = GetIssueCategory(issue, origin, normalizedMessage);
                occurrences.Add(new IssueOccurrence(
                    category,
                    normalizedMessage,
                    GetIssueTitle(issue, normalizedMessage),
                    BuildIssueDetail(issue),
                    issue.Severity,
                    hasTargetNode ? targetNodeId : string.Empty,
                    hasTargetNode ? DependencyIconProvider.GetIcon(targetNode) : null,
                    hasTargetNode
                        ? DependencyNodeStyleResolver.GetNodeAccentColor(targetNode)
                        : new Color(0.38f, 0.42f, 0.47f),
                    null,
                    origin,
                    BuildLocation(hasTargetNode ? targetNode : null, string.Empty, issue),
                    issue.OccurrenceCount));
            }
        }

        private static IssueGroup CreateGroup(GroupKey key, List<IssueOccurrence> occurrences)
        {
            var first = occurrences[0];
            var icon = first.GroupIcon;
            for (var i = 1; i < occurrences.Count; i++)
            {
                if (occurrences[i].GroupIcon != icon)
                {
                    icon = null;
                    break;
                }
            }

            return new IssueGroup(
                key.Id,
                first.GroupTitle,
                key.Severity,
                key.Origin,
                icon,
                occurrences);
        }

        private static string GetMissingReferenceType(DependencyNode node)
        {
            var typeName = node == null ? string.Empty : node.TypeName;
            if (string.Equals(typeName, "Script", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "MonoBehaviour", StringComparison.OrdinalIgnoreCase))
            {
                return "Script";
            }

            if (string.Equals(typeName, "Object", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "Missing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "Missing Reference", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(typeName))
            {
                return "Object Reference";
            }

            return typeName;
        }

        private static string GetMissingReferenceTitle(string missingType)
        {
            if (string.Equals(missingType, "Script", StringComparison.Ordinal))
            {
                return "Missing Script";
            }

            if (string.Equals(missingType, "Object Reference", StringComparison.Ordinal))
            {
                return "Missing Object Reference";
            }

            return "Missing Reference: " + missingType;
        }

        private static string GetIssueCategory(
            DependencyScanIssue issue,
            IssueOrigin origin,
            string normalizedMessage)
        {
            if (origin == IssueOrigin.Console)
            {
                var compilerCode = CompilerCodeRegex.Match(normalizedMessage);
                return compilerCode.Success
                    ? "Unity Console/Compiler/" + compilerCode.Value
                    : "Unity Console";
            }

            return string.IsNullOrEmpty(issue.ScannerName) ? "Analyzer" : issue.ScannerName;
        }

        private static string GetIssueTitle(DependencyScanIssue issue, string normalizedMessage)
        {
            if (!string.IsNullOrEmpty(normalizedMessage))
            {
                return IssueTextFormatter.FormatTitle(normalizedMessage);
            }

            return IssueTextFormatter.FormatTitle(issue.ScannerName);
        }

        private static string NormalizeMessage(DependencyScanIssue issue)
        {
            var message = issue == null ? string.Empty : issue.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            message = message.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (!string.IsNullOrEmpty(issue.FilePath)
                && message.StartsWith(issue.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                message = message.Substring(issue.FilePath.Length);
                message = CompilerLocationPrefixRegex.Replace(message, string.Empty);
            }

            return WhitespaceRegex.Replace(message, " ").Trim();
        }

        private static IssueLocation BuildLocation(
            DependencyNode node,
            string leafLabel,
            DependencyScanIssue issue)
        {
            if (issue != null && !string.IsNullOrEmpty(issue.FilePath))
            {
                return new IssueLocation(null, BuildFileLocation(issue));
            }

            if (node == null)
            {
                return new IssueLocation(null, "No related node");
            }

            var path = (node.Path ?? string.Empty).Replace('\\', '/');
            var sceneSeparator = path.IndexOf("::", StringComparison.Ordinal);
            if (sceneSeparator >= 0)
            {
                var scenePath = path.Substring(0, sceneSeparator);
                var hierarchyPath = path.Substring(sceneSeparator + 2);
                var segments = new List<string> { "Scene: " + GetSceneDisplayName(scenePath) };
                segments.AddRange(SplitPath(hierarchyPath));
                return BuildHierarchicalLocation(segments, leafLabel, node.DisplayName);
            }

            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return BuildHierarchicalLocation(SplitPath(path), leafLabel, node.DisplayName);
            }

            var fallbackSegments = SplitPath(path);
            return fallbackSegments.Count == 0
                ? new IssueLocation(null, string.IsNullOrEmpty(leafLabel) ? node.DisplayName : leafLabel)
                : BuildHierarchicalLocation(fallbackSegments, leafLabel, node.DisplayName);
        }

        private static IssueLocation BuildHierarchicalLocation(
            List<string> pathSegments,
            string leafLabel,
            string fallbackLabel)
        {
            if (!string.IsNullOrEmpty(leafLabel))
            {
                return new IssueLocation(pathSegments, leafLabel);
            }

            if (pathSegments.Count == 0)
            {
                return new IssueLocation(null, fallbackLabel);
            }

            var label = pathSegments[pathSegments.Count - 1];
            pathSegments.RemoveAt(pathSegments.Count - 1);
            return new IssueLocation(pathSegments, label);
        }

        private static List<string> SplitPath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? new List<string>()
                : path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private static string GetSceneDisplayName(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                return "Unsaved Scene";
            }

            var displayName = Path.GetFileNameWithoutExtension(scenePath);
            return string.IsNullOrEmpty(displayName) ? scenePath : displayName;
        }

        private static string BuildFileLocation(DependencyScanIssue issue)
        {
            var location = issue.FilePath;
            if (issue.Line > 0)
            {
                location += ":" + issue.Line;
                if (issue.Column > 0)
                {
                    location += ":" + issue.Column;
                }
            }

            return location;
        }

        private static string BuildIssueDetail(DependencyScanIssue issue)
        {
            var parts = new List<string>();
            var message = IssueTextFormatter.FormatDetail(issue.Message);
            if (!string.IsNullOrEmpty(message))
            {
                parts.Add(message);
            }

            if (!string.IsNullOrEmpty(issue.FilePath))
            {
                parts.Add("File: " + BuildFileLocation(issue));
            }

            if (issue.OccurrenceCount > 1)
            {
                parts.Add("Occurrences: " + issue.OccurrenceCount);
            }

            if (!string.IsNullOrEmpty(issue.StackTrace))
            {
                parts.Add("Stack Trace:\n" + issue.StackTrace.Trim());
            }

            return string.Join("\n", parts);
        }

        private static IssueOrigin GetOrigin(DependencyScanIssue issue)
        {
            return issue != null
                && string.Equals(issue.ScannerName, "Unity Console", StringComparison.Ordinal)
                    ? IssueOrigin.Console
                    : IssueOrigin.Analyzer;
        }

        private static bool IsDisplayedSeverity(DependencyScanIssueSeverity severity)
        {
            return severity == DependencyScanIssueSeverity.Error
                || severity == DependencyScanIssueSeverity.Warning;
        }

        private readonly struct GroupKey : IEquatable<GroupKey>
        {
            public GroupKey(
                IssueOrigin origin,
                DependencyScanIssueSeverity severity,
                string category,
                string normalizedMessage)
            {
                Origin = origin;
                Severity = severity;
                Category = category ?? string.Empty;
                NormalizedMessage = normalizedMessage ?? string.Empty;
            }

            public IssueOrigin Origin { get; }
            public DependencyScanIssueSeverity Severity { get; }
            public string Category { get; }
            public string NormalizedMessage { get; }
            public string Id => Origin + "\u001f" + Severity + "\u001f" + Category + "\u001f" + NormalizedMessage;

            public bool Equals(GroupKey other)
            {
                return Origin == other.Origin
                    && Severity == other.Severity
                    && string.Equals(Category, other.Category, StringComparison.Ordinal)
                    && string.Equals(NormalizedMessage, other.NormalizedMessage, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is GroupKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = (int)Origin;
                    hash = (hash * 397) ^ (int)Severity;
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Category);
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(NormalizedMessage);
                    return hash;
                }
            }
        }
    }
}
