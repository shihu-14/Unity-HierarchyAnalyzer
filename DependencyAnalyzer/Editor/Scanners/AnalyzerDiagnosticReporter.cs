using System;
using System.Collections.Generic;
using System.Text;
using DependencyAnalyzer.Editor.Core;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Scanners
{
    internal static class AnalyzerDiagnosticReporter
    {
        private const string Prefix = "[Dependency Analyzer Diagnostic]";

        internal static void Report(IReadOnlyList<DependencyScanIssue> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
            {
                return;
            }

            var reportedKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                if (diagnostic == null || !reportedKeys.Add(BuildKey(diagnostic)))
                {
                    continue;
                }

                var message = BuildMessage(diagnostic);
                switch (diagnostic.Severity)
                {
                    case DependencyScanIssueSeverity.Error:
                        Debug.LogError(message);
                        break;
                    case DependencyScanIssueSeverity.Warning:
                        Debug.LogWarning(message);
                        break;
                    default:
                        Debug.Log(message);
                        break;
                }
            }
        }

        private static string BuildKey(DependencyScanIssue diagnostic)
        {
            return diagnostic.ScannerName + "\u001f"
                + diagnostic.SubjectPath + "\u001f"
                + diagnostic.Message + "\u001f"
                + diagnostic.Severity;
        }

        private static string BuildMessage(DependencyScanIssue diagnostic)
        {
            var builder = new StringBuilder(Prefix);
            if (!string.IsNullOrWhiteSpace(diagnostic.ScannerName))
            {
                builder.Append(' ').Append(diagnostic.ScannerName);
            }

            if (!string.IsNullOrWhiteSpace(diagnostic.SubjectPath))
            {
                builder.Append(" [").Append(diagnostic.SubjectPath).Append(']');
            }

            if (!string.IsNullOrWhiteSpace(diagnostic.Message))
            {
                builder.Append(": ").Append(diagnostic.Message);
            }

            if (!string.IsNullOrWhiteSpace(diagnostic.StackTrace))
            {
                builder.AppendLine().Append(diagnostic.StackTrace.Trim());
            }

            return builder.ToString();
        }
    }
}
