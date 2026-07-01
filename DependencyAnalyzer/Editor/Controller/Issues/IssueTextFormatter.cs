using System;
using System.Text;

namespace DependencyAnalyzer.Editor.Controller.Issues
{
    internal static class IssueTextFormatter
    {
        public static string FormatTitle(string value)
        {
            value = TrimIssuePrefix(value);
            return string.IsNullOrEmpty(value)
                ? "Issue"
                : value.Replace('_', ' ');
        }

        public static string FormatDetail(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var result = value.Trim();
            result = ReplaceIgnoreCase(result, "Assets/DependencyAnalyzerDemo/IssueAssets/", string.Empty);
            result = ReplaceIgnoreCase(result, "Issue_", string.Empty);
            result = ReplaceIgnoreCase(result, "IssueConsoleWarningBehaviour", "ConsoleWarningBehaviour");
            result = ReplaceIgnoreCase(result, "IssueConsoleErrorShader", "ConsoleErrorShader");
            result = ReplaceIgnoreCase(result, "Analyzer demo warning: ", string.Empty);
            result = ReplaceIgnoreCase(result, "Console demo warning: ", string.Empty);
            return result;
        }

        private static string TrimIssuePrefix(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var result = value.Trim();
            while (result.StartsWith("Issue_", StringComparison.OrdinalIgnoreCase))
            {
                result = result.Substring("Issue_".Length);
            }

            if (result.Length > "Issue".Length
                && result.StartsWith("Issue", StringComparison.Ordinal)
                && char.IsUpper(result["Issue".Length]))
            {
                result = result.Substring("Issue".Length);
            }

            return result;
        }

        private static string ReplaceIgnoreCase(string value, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(oldValue))
            {
                return value ?? string.Empty;
            }

            var builder = new StringBuilder();
            var searchStart = 0;
            while (true)
            {
                var index = value.IndexOf(oldValue, searchStart, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    builder.Append(value, searchStart, value.Length - searchStart);
                    return builder.ToString();
                }

                builder.Append(value, searchStart, index - searchStart);
                builder.Append(newValue);
                searchStart = index + oldValue.Length;
            }
        }
    }
}
