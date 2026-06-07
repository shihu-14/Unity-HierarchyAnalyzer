using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using DependencyAnalyzer.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Scanners
{
    internal static class ConsoleIssueScanner
    {
        private const string ScannerName = "Unity Console";
        private const int MaxConsoleEntries = 500;
        private const int ErrorModeMask = 1 | 2 | 16 | 64 | 2048 | 8192;
        private const int WarningModeMask = 128 | 16384 | 32768;
        private static readonly Regex AssetPathRegex = new Regex(
            @"Assets/[^\r\n\(\):]+?\.(?:cs|shader|compute|asmdef|asmref|prefab|unity|mat|asset|fbx|obj|dae|blend|png|jpg|jpeg|tga|psd|wav|mp3|ogg|anim|controller|overrideController)",
            RegexOptions.IgnoreCase);

        public static void AddConsoleIssues(DependencyGraphData graph, DependencyCache cache)
        {
            if (graph == null || cache == null)
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (var entry in ReadConsoleEntries())
                {
                    if (!TryGetSeverity(entry, out var severity)
                        || IsAnalyzerGeneratedLog(entry.Condition))
                    {
                        continue;
                    }

                    var targetNode = ResolveTargetNode(graph, cache, entry);
                    if (targetNode == null)
                    {
                        continue;
                    }

                    var subjectPath = string.IsNullOrEmpty(targetNode.Path) ? targetNode.Id : targetNode.Path;
                    var message = BuildIssueMessage(entry);
                    var key = severity + "\n" + subjectPath + "\n" + message;
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    graph.AddIssue(new DependencyScanIssueData(
                        ScannerName,
                        subjectPath,
                        message,
                        severity));
                }
            }
            catch
            {
                // Unity's console entry API is internal and can change between editor versions.
            }
        }

        private static IEnumerable<ConsoleLogEntry> ReadConsoleEntries()
        {
            var editorAssembly = typeof(EditorWindow).Assembly;
            var logEntriesType = editorAssembly.GetType("UnityEditor.LogEntries");
            var logEntryType = editorAssembly.GetType("UnityEditor.LogEntry");
            if (logEntriesType == null || logEntryType == null)
            {
                yield break;
            }

            var getCount = logEntriesType.GetMethod(
                "GetCount",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var getEntry = FindGetEntryMethod(logEntriesType);
            if (getCount == null || getEntry == null)
            {
                yield break;
            }

            var start = logEntriesType.GetMethod(
                "StartGettingEntries",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var end = logEntriesType.GetMethod(
                "EndGettingEntries",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            start?.Invoke(null, null);
            try
            {
                var countObject = getCount.Invoke(null, null);
                var count = countObject is int ? (int)countObject : 0;
                var startIndex = Mathf.Max(0, count - MaxConsoleEntries);
                for (var i = startIndex; i < count; i++)
                {
                    var entryObject = Activator.CreateInstance(logEntryType, true);
                    var args = new[] { (object)i, entryObject };
                    getEntry.Invoke(null, args);
                    entryObject = args[1];
                    yield return new ConsoleLogEntry(
                        GetStringValue(logEntryType, entryObject, "condition"),
                        GetStringValue(logEntryType, entryObject, "file"),
                        GetStringValue(logEntryType, entryObject, "stackTrace"),
                        GetIntValue(logEntryType, entryObject, "line"),
                        GetIntValue(logEntryType, entryObject, "mode"),
                        GetIntValue(logEntryType, entryObject, "instanceID", "instanceId"));
                }
            }
            finally
            {
                end?.Invoke(null, null);
            }
        }

        private static MethodInfo FindGetEntryMethod(Type logEntriesType)
        {
            foreach (var method in logEntriesType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != "GetEntryInternal")
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 2)
                {
                    return method;
                }
            }

            return null;
        }

        private static bool TryGetSeverity(ConsoleLogEntry entry, out DependencyScanIssueSeverity severity)
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

        private static DependencyNodeData ResolveTargetNode(
            DependencyGraphData graph,
            DependencyCache cache,
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

            return graph.AddOrUpdateNode(AssetScanner.CreateAssetNode(assetPath, cache));
        }

        private static DependencyNodeData FindNodeByInstanceId(DependencyGraphData graph, int instanceId)
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

        private static DependencyNodeData FindNodeByPath(DependencyGraphData graph, string assetPath)
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
            var message = FirstLine(entry.Condition);
            var path = FindAssetPath(entry);
            if (!string.IsNullOrEmpty(path)
                && entry.Line > 0
                && message.IndexOf(path, StringComparison.OrdinalIgnoreCase) < 0)
            {
                message = path + "(" + entry.Line + "): " + message;
            }

            return string.IsNullOrEmpty(message) ? "Console issue" : message;
        }

        private static string FirstLine(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var normalized = value.Replace("\r\n", "\n");
            var newline = normalized.IndexOf('\n');
            return (newline >= 0 ? normalized.Substring(0, newline) : normalized).Trim();
        }

        private static bool IsAnalyzerGeneratedLog(string condition)
        {
            if (string.IsNullOrEmpty(condition))
            {
                return false;
            }

            return condition.IndexOf("[Serialized Property Scanner]", StringComparison.OrdinalIgnoreCase) >= 0
                || condition.IndexOf("[Unity Console]", StringComparison.OrdinalIgnoreCase) >= 0
                || condition.IndexOf("Dependency Analyzer", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetStringValue(Type type, object instance, string name)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(instance) as string ?? string.Empty;
            }

            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property == null ? string.Empty : property.GetValue(instance, null) as string ?? string.Empty;
        }

        private static int GetIntValue(Type type, object instance, params string[] names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                var field = type.GetField(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return ConvertToInt(field.GetValue(instance));
                }

                var property = type.GetProperty(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    return ConvertToInt(property.GetValue(instance, null));
                }
            }

            return 0;
        }

        private static int ConvertToInt(object value)
        {
            if (value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        private struct ConsoleLogEntry
        {
            public ConsoleLogEntry(string condition, string file, string stackTrace, int line, int mode, int instanceId)
            {
                Condition = condition ?? string.Empty;
                File = file ?? string.Empty;
                StackTrace = stackTrace ?? string.Empty;
                Line = line;
                Mode = mode;
                InstanceId = instanceId;
            }

            public string Condition { get; }
            public string File { get; }
            public string StackTrace { get; }
            public int Line { get; }
            public int Mode { get; }
            public int InstanceId { get; }
        }
    }
}
