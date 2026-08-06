using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Scanners.Issues;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Scanners
{
    internal static class ConsoleIssueScanner
    {
        private const string ScannerName = "Unity Console";
        private const string ReaderScannerName = "Unity Console Reader";
        private static readonly Regex AssetPathRegex = new Regex(
            @"Assets/[^\r\n\(\):]+?\.(?:cs|shader|compute|asmdef|asmref|prefab|unity|mat|asset|fbx|obj|dae|blend|png|jpg|jpeg|tga|psd|wav|mp3|ogg|anim|controller|overrideController)",
            RegexOptions.IgnoreCase);

        public static void AddConsoleIssues(DependencyGraph graph, DependencyNodeCache cache)
        {
            AddConsoleIssues(graph, cache, new UnityConsoleLogReader());
        }

        internal static void AddConsoleIssues(
            DependencyGraph graph,
            DependencyNodeCache cache,
            IConsoleLogReader reader)
        {
            if (graph == null || cache == null)
            {
                return;
            }

            ConsoleLogReadResult readResult;
            try
            {
                readResult = reader == null
                    ? ConsoleLogReadResult.Failure("Console reader was not provided.")
                    : reader.Read();
            }
            catch (Exception exception)
            {
                readResult = ConsoleLogReadResult.Failure(exception.GetType().Name + ": " + exception.Message);
            }

            if (readResult == null || !readResult.Succeeded)
            {
                AddReaderFailureIssue(graph, readResult == null ? "Console reader returned no result." : readResult.ErrorMessage);
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < readResult.Entries.Count; i++)
            {
                AddIssueFromEntry(graph, cache, seen, readResult.Entries[i]);
            }
        }

        private static void AddReaderFailureIssue(DependencyGraph graph, string errorMessage)
        {
            graph.AddIssue(new DependencyScanIssue(
                ReaderScannerName,
                string.Empty,
                "Unable to read the current Unity Console: "
                    + (string.IsNullOrEmpty(errorMessage) ? "Unknown reader failure." : errorMessage),
                DependencyScanIssueSeverity.Warning));
        }

        private static void AddIssueFromEntry(
            DependencyGraph graph,
            DependencyNodeCache cache,
            HashSet<string> seen,
            ConsoleLogEntry entry)
        {
            if (!ConsoleIssueParser.TryGetSeverity(entry, out var severity)
                || IsAnalyzerGeneratedLog(entry.Condition))
            {
                return;
            }

            var targetNode = ResolveTargetNode(graph, cache, entry);
            var subjectPath = targetNode == null
                ? string.Empty
                : string.IsNullOrEmpty(targetNode.Path) ? targetNode.Id : targetNode.Path;
            var message = BuildIssueMessage(entry);
            var key = BuildConsoleEntryKey(entry, severity, subjectPath, message);
            if (!seen.Add(key))
            {
                return;
            }

            graph.AddIssue(new DependencyScanIssue(
                ScannerName,
                subjectPath,
                message,
                severity,
                entry.File,
                entry.Line,
                entry.Column,
                entry.StackTrace,
                entry.InstanceId,
                entry.OccurrenceCount));
        }

        private static string BuildConsoleEntryKey(
            ConsoleLogEntry entry,
            DependencyScanIssueSeverity severity,
            string subjectPath,
            string message)
        {
            if (entry.GlobalLineIndex >= 0)
            {
                return "global:" + entry.GlobalLineIndex;
            }

            if (entry.RowIndex >= 0)
            {
                return "row:" + entry.RowIndex;
            }

            return severity + "\n"
                + subjectPath + "\n"
                + message + "\n"
                + entry.File + "\n"
                + entry.Line + ":" + entry.Column + "\n"
                + entry.StackTrace;
        }

        private static DependencyNode ResolveTargetNode(
            DependencyGraph graph,
            DependencyNodeCache cache,
            ConsoleLogEntry entry)
        {
            var node = FindNodeByInstanceId(graph, entry.InstanceId);
            if (node != null)
            {
                return node;
            }

            var assetPath = FindAssetPath(entry);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            node = FindNodeByPath(graph, assetPath);
            if (node != null)
            {
                return node;
            }

            if (!AssetExists(assetPath))
            {
                return null;
            }

            return graph.AddOrUpdateNode(AssetNodeFactory.CreateAssetNode(assetPath, cache));
        }

        private static DependencyNode FindNodeByInstanceId(DependencyGraph graph, int instanceId)
        {
            if (instanceId == 0)
            {
                return null;
            }

            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                if (node.InstanceId == instanceId)
                {
                    return node;
                }
            }

            var contextObject = EditorUtility.InstanceIDToObject(instanceId);
            var assetPath = contextObject == null ? string.Empty : AssetDatabase.GetAssetPath(contextObject);
            return string.IsNullOrEmpty(assetPath) ? null : FindNodeByPath(graph, assetPath);
        }

        private static DependencyNode FindNodeByPath(DependencyGraph graph, string assetPath)
        {
            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                if (string.Equals(node.Path, assetPath, StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }
            }

            return null;
        }

        private static string FindAssetPath(ConsoleLogEntry entry)
        {
            var path = NormalizeAssetPath(entry.File);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            path = ExtractAssetPath(entry.Condition);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            return ExtractAssetPath(entry.StackTrace);
        }

        private static string ExtractAssetPath(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var match = AssetPathRegex.Match(text.Replace('\\', '/'));
            return match.Success ? NormalizeAssetPath(match.Value) : string.Empty;
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            path = path.Replace('\\', '/').Trim(' ', '\t', '\r', '\n', '\'', '"');
            var assetsRoot = Application.dataPath.Replace('\\', '/');
            if (path.StartsWith(assetsRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets/" + path.Substring(assetsRoot.Length + 1);
            }

            var assetsIndex = path.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIndex >= 0)
            {
                return path.Substring(assetsIndex + 1);
            }

            return path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ? path : string.Empty;
        }

        private static bool AssetExists(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null
                || AssetDatabase.GetMainAssetTypeAtPath(assetPath) != null)
            {
                return true;
            }

            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            return File.Exists(absolutePath);
        }

        private static string BuildIssueMessage(ConsoleLogEntry entry)
        {
            var message = ConsoleIssueParser.NormalizeConsoleMessage(entry.Condition);
            if (!string.IsNullOrEmpty(message))
            {
                return message;
            }

            var path = FindAssetPath(entry);
            if (!string.IsNullOrEmpty(path) && entry.Line > 0)
            {
                return path + "(" + entry.Line + ")";
            }

            return "Console issue";
        }

        private static bool IsAnalyzerGeneratedLog(string condition)
        {
            if (string.IsNullOrEmpty(condition))
            {
                return false;
            }

            return condition.IndexOf("[Serialized Property Scanner]", StringComparison.OrdinalIgnoreCase) >= 0
                || condition.IndexOf("[Unity Console]", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
