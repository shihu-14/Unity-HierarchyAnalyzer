namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal readonly struct IssuePanelSummary
    {
        public IssuePanelSummary(
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
        public int TotalCount => ErrorCount + WarningCount;
    }
}
