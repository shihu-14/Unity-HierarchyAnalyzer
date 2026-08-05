using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Utils;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Controller.Issues
{
    internal static class IssuePanelEntryBuilder
    {
        public static int Count(DependencyGraphData graphData)
        {
            return Build(graphData).Count;
        }

        public static List<IssuePanelEntry> Build(DependencyGraphData graphData)
        {
            var entries = new List<IssuePanelEntry>();
            if (graphData == null)
            {
                return entries;
            }

            var missingTargetIds = new HashSet<string>(
                graphData.Edges
                    .Where(edge => edge != null && edge.PointsToMissingReference)
                    .Select(edge => edge.TargetNodeId),
                StringComparer.Ordinal);
            var missingNodes = graphData.Nodes
                .Where(node => node.Kind == DependencyNodeKind.MissingReference || missingTargetIds.Contains(node.Id))
                .OrderBy(node => node.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase);
            foreach (var node in missingNodes)
            {
                entries.Add(new IssuePanelEntry(
                    "Missing Reference: " + IssueTextFormatter.FormatTitle(node.DisplayName),
                    IssueTextFormatter.FormatDetail(string.IsNullOrEmpty(node.Path) ? node.TypeName : node.Path),
                    DependencyScanIssueSeverity.Warning,
                    node.Id,
                    IconUtility.GetIcon(node),
                    IconUtility.GetNodeAccentColor(node),
                    IssuePanelEntryOrigin.Analyzer));
            }

            foreach (var issue in graphData.Issues)
            {
                if (!IsDisplayedSeverity(issue.Severity))
                {
                    continue;
                }

                var targetNodeId = IssueTargetResolver.FindIssueEntryTargetNodeId(graphData, issue);
                if (string.IsNullOrEmpty(targetNodeId)
                    || !graphData.TryGetNode(targetNodeId, out var targetNode))
                {
                    if (ShouldDisplayWithoutTarget(issue))
                    {
                        entries.Add(new IssuePanelEntry(
                            IssueTextFormatter.FormatTitle(issue.ScannerName) + " (No related node)",
                            BuildIssueDetail(issue),
                            issue.Severity,
                            string.Empty,
                            null,
                            new Color(0.38f, 0.42f, 0.47f),
                            GetOrigin(issue)));
                    }

                    continue;
                }

                entries.Add(new IssuePanelEntry(
                    IssueTextFormatter.FormatTitle(targetNode.DisplayName),
                    BuildIssueDetail(issue),
                    issue.Severity,
                    targetNodeId,
                    IconUtility.GetIcon(targetNode),
                    IconUtility.GetNodeAccentColor(targetNode),
                    GetOrigin(issue)));
            }

            return entries
                .OrderByDescending(entry => entry.Severity == DependencyScanIssueSeverity.Error)
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Detail, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IssuePanelCounts CountByOrigin(IReadOnlyList<IssuePanelEntry> entries)
        {
            var consoleErrors = 0;
            var consoleWarnings = 0;
            var analyzerErrors = 0;
            var analyzerWarnings = 0;
            if (entries != null)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var isError = entry.Severity == DependencyScanIssueSeverity.Error;
                    if (entry.Origin == IssuePanelEntryOrigin.Console)
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

            return new IssuePanelCounts(
                consoleErrors,
                consoleWarnings,
                analyzerErrors,
                analyzerWarnings);
        }

        private static string BuildIssueDetail(DependencyScanIssueData issue)
        {
            var parts = new List<string>();
            var message = IssueTextFormatter.FormatDetail(issue.Message);
            if (!string.IsNullOrEmpty(message))
            {
                parts.Add(message);
            }

            if (!string.IsNullOrEmpty(issue.FilePath))
            {
                var location = "File: " + issue.FilePath;
                if (issue.Line > 0)
                {
                    location += ":" + issue.Line;
                    if (issue.Column > 0)
                    {
                        location += ":" + issue.Column;
                    }
                }

                parts.Add(location);
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

        private static IssuePanelEntryOrigin GetOrigin(DependencyScanIssueData issue)
        {
            return IsConsoleSnapshotIssue(issue)
                ? IssuePanelEntryOrigin.Console
                : IssuePanelEntryOrigin.Analyzer;
        }

        private static bool IsConsoleSnapshotIssue(DependencyScanIssueData issue)
        {
            return issue != null
                && string.Equals(issue.ScannerName, "Unity Console", StringComparison.Ordinal);
        }

        private static bool ShouldDisplayWithoutTarget(DependencyScanIssueData issue)
        {
            return issue != null
                && (string.Equals(issue.ScannerName, "Unity Console", StringComparison.Ordinal)
                    || string.Equals(issue.ScannerName, "Unity Console Reader", StringComparison.Ordinal));
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

        private static bool IsDisplayedSeverity(DependencyScanIssueSeverity severity)
        {
            return severity == DependencyScanIssueSeverity.Error
                || severity == DependencyScanIssueSeverity.Warning;
        }
    }
}
