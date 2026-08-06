using DependencyAnalyzer.Editor.Core;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Controller.Issues
{
    internal enum IssuePanelEntryOrigin
    {
        Analyzer,
        Console
    }

    internal readonly struct IssuePanelCounts
    {
        public IssuePanelCounts(
            int consoleErrors,
            int consoleWarnings,
            int analyzerErrors,
            int analyzerWarnings)
        {
            ConsoleErrors = consoleErrors;
            ConsoleWarnings = consoleWarnings;
            AnalyzerErrors = analyzerErrors;
            AnalyzerWarnings = analyzerWarnings;
        }

        public int ConsoleErrors { get; }
        public int ConsoleWarnings { get; }
        public int AnalyzerErrors { get; }
        public int AnalyzerWarnings { get; }
        public int ErrorCount => ConsoleErrors + AnalyzerErrors;
        public int WarningCount => ConsoleWarnings + AnalyzerWarnings;
        public string DisplayText => "Issues | Console E: " + ConsoleErrors
            + " W: " + ConsoleWarnings
            + " | Analyzer E: " + AnalyzerErrors
            + " W: " + AnalyzerWarnings;
    }

    internal sealed class IssuePanelEntry
    {
        public IssuePanelEntry(
            string title,
            string detail,
            DependencyScanIssueSeverity severity,
            string targetNodeId,
            Texture nodeIcon,
            Color nodeColor,
            IssuePanelEntryOrigin origin)
        {
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            Severity = severity;
            TargetNodeId = targetNodeId ?? string.Empty;
            NodeIcon = nodeIcon;
            NodeColor = nodeColor;
            Origin = origin;
        }

        public string Title { get; }
        public string Detail { get; }
        public DependencyScanIssueSeverity Severity { get; }
        public string TargetNodeId { get; }
        public Texture NodeIcon { get; }
        public Color NodeColor { get; }
        public IssuePanelEntryOrigin Origin { get; }
    }
}
