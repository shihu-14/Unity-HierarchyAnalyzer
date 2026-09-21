using System;

namespace DependencyAnalyzer.Editor.Core
{
    public enum DependencyScanIssueSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Describes an Analyzer diagnostic produced when a scanner cannot fully inspect Unity data.
    /// These diagnostics are not project issues and are not shown in the Issues panel.
    /// </summary>
    [Serializable]
    public sealed class DependencyScanIssue
    {
        public DependencyScanIssue(
            string scannerName,
            string subjectPath,
            string message,
            DependencyScanIssueSeverity severity)
            : this(scannerName, subjectPath, message, severity, string.Empty, 0, 0, string.Empty, 0, 1)
        {
        }

        public DependencyScanIssue(
            string scannerName,
            string subjectPath,
            string message,
            DependencyScanIssueSeverity severity,
            string filePath,
            int line,
            int column,
            string stackTrace,
            int instanceId,
            int occurrenceCount)
        {
            ScannerName = scannerName ?? string.Empty;
            SubjectPath = subjectPath ?? string.Empty;
            Message = message ?? string.Empty;
            Severity = severity;
            FilePath = filePath ?? string.Empty;
            Line = Math.Max(0, line);
            Column = Math.Max(0, column);
            StackTrace = stackTrace ?? string.Empty;
            InstanceId = instanceId;
            OccurrenceCount = Math.Max(1, occurrenceCount);
        }

        public string ScannerName { get; }
        public string SubjectPath { get; }
        public string Message { get; }
        public DependencyScanIssueSeverity Severity { get; }
        public string FilePath { get; }
        public int Line { get; }
        public int Column { get; }
        public string StackTrace { get; }
        public int InstanceId { get; }
        public int OccurrenceCount { get; }
    }
}
