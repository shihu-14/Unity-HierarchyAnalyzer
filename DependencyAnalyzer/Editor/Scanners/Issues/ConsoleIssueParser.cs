using System;
using DependencyAnalyzer.Editor.Core;

namespace DependencyAnalyzer.Editor.Scanners.Issues
{
    internal static class ConsoleIssueParser
    {
        private const int ErrorModeMask = 1 | 2 | 16 | 64 | 2048 | 8192;
        private const int WarningModeMask = 128 | 16384 | 32768;

        public static bool TryGetSeverity(ConsoleLogEntry entry, out DependencyScanIssueSeverity severity)
        {
            if ((entry.Mode & ErrorModeMask) != 0)
            {
                severity = DependencyScanIssueSeverity.Error;
                return true;
            }

            if ((entry.Mode & WarningModeMask) != 0)
            {
                severity = DependencyScanIssueSeverity.Warning;
                return true;
            }

            var text = entry.Condition ?? string.Empty;
            if (text.IndexOf(": error ", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf(" error CS", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("shader error", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                severity = DependencyScanIssueSeverity.Error;
                return true;
            }

            if (text.IndexOf(": warning ", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf(" warning CS", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                severity = DependencyScanIssueSeverity.Warning;
                return true;
            }

            severity = DependencyScanIssueSeverity.Warning;
            return false;
        }

        public static int ExtractLineNumber(string text, string assetPath)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(assetPath))
            {
                return 0;
            }

            var pathIndex = text.IndexOf(assetPath, StringComparison.OrdinalIgnoreCase);
            if (pathIndex < 0)
            {
                return 0;
            }

            var lineStart = pathIndex + assetPath.Length;
            if (lineStart >= text.Length || text[lineStart] != '(')
            {
                return 0;
            }

            lineStart++;
            var lineEnd = lineStart;
            while (lineEnd < text.Length && char.IsDigit(text[lineEnd]))
            {
                lineEnd++;
            }

            return lineEnd > lineStart && int.TryParse(text.Substring(lineStart, lineEnd - lineStart), out var line)
                ? line
                : 0;
        }

        public static string NormalizeConsoleMessage(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\r\n", "\n").Trim();
        }
    }
}
