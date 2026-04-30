using System;

namespace DependencyAnalyzer.Editor.Core
{
    public enum DependencyScanIssueSeverity
    {
        Info,
        Warning,
        Error
    }

    [Serializable]
    public sealed class DependencyScanIssueData
    {
        public DependencyScanIssueData(
            string scannerName,
            string subjectPath,
            string message,
            DependencyScanIssueSeverity severity)
        {
            ScannerName = scannerName ?? string.Empty;
            SubjectPath = subjectPath ?? string.Empty;
            Message = message ?? string.Empty;
            Severity = severity;
        }

        public string ScannerName { get; }
        public string SubjectPath { get; }
        public string Message { get; }
        public DependencyScanIssueSeverity Severity { get; }
    }
}
