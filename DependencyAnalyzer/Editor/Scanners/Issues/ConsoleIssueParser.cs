using System;
using DependencyAnalyzer.Editor.Core;

namespace DependencyAnalyzer.Editor.Scanners.Issues
{
    internal static class ConsoleIssueParser
    {
        // ConsoleWindow excludes bit 22 from its error styles; LogMessageFlags also uses it for postprocessed stack traces.
        private const int ErrorModeMask =
            (1 << 0)  // Error
            | (1 << 1)  // Assert
            | (1 << 4)  // Fatal
            | (1 << 6)  // AssetImportError
            | (1 << 8)  // ScriptingError
            | (1 << 11) // ScriptCompileError
            | (1 << 17) // ScriptingException
            | (1 << 20) // GraphCompileError
            | (1 << 21); // ScriptingAssertion
        private const int WarningModeMask =
            (1 << 7)  // AssetImportWarning
            | (1 << 9)  // ScriptingWarning
            | (1 << 12); // ScriptCompileWarning
        private const int LogModeMask =
            (1 << 2)  // Log
            | (1 << 10); // ScriptingLog

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

            if ((entry.Mode & LogModeMask) != 0)
            {
                severity = DependencyScanIssueSeverity.Warning;
                return false;
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
