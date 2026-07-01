using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
        private const int MaxConsoleEntries = 500;
        private const int MaxEditorLogBytes = 1024 * 1024;
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
                    AddIssueFromEntry(graph, cache, seen, entry);
                }
            }
            catch
            {
                // Unity's console entry API is internal and can change between editor versions.
            }

            try
            {
                foreach (var entry in ReadEditorLogEntries())
                {
                    AddIssueFromEntry(graph, cache, seen, entry);
                }
            }
            catch
            {
                // Editor.log is a fallback for compiler and shader messages that may not be exposed by LogEntries.
            }
        }

        private static void AddIssueFromEntry(
            DependencyGraphData graph,
            DependencyCache cache,
            HashSet<string> seen,
            ConsoleLogEntry entry)
        {
            if (!ConsoleIssueParser.TryGetSeverity(entry, out var severity)
                || IsAnalyzerGeneratedLog(entry.Condition))
            {
                return;
            }

            var targetNode = ResolveTargetNode(graph, cache, entry);
            if (targetNode == null)
            {
                return;
            }

            var subjectPath = string.IsNullOrEmpty(targetNode.Path) ? targetNode.Id : targetNode.Path;
            var message = BuildIssueMessage(entry);
            var key = severity + "\n" + subjectPath + "\n" + message;
            if (!seen.Add(key))
            {
                return;
            }

            graph.AddIssue(new DependencyScanIssueData(
                ScannerName,
                subjectPath,
                message,
                severity));
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

        private static IEnumerable<ConsoleLogEntry> ReadEditorLogEntries()
        {
            foreach (var line in ReadRecentEditorLogLines())
            {
                var condition = ConsoleIssueParser.NormalizeConsoleMessage(line);
                if (string.IsNullOrEmpty(condition))
                {
                    continue;
                }

                var assetPath = ExtractAssetPath(condition);
                if (string.IsNullOrEmpty(assetPath) || IsIgnoredEditorLogAssetPath(assetPath))
                {
                    continue;
                }

                var entry = new ConsoleLogEntry(
                    condition,
                    assetPath,
                    string.Empty,
                    ConsoleIssueParser.ExtractLineNumber(condition, assetPath),
                    0,
                    0);
                if (ConsoleIssueParser.TryGetSeverity(entry, out _))
                {
                    yield return entry;
                }
            }
        }

        private static IEnumerable<string> ReadRecentEditorLogLines()
        {
            var logPath = GetEditorLogPath();
            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
            {
                yield break;
            }

            using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var start = Math.Max(0L, stream.Length - MaxEditorLogBytes);
                stream.Seek(start, SeekOrigin.Begin);
                using (var reader = new StreamReader(stream))
                {
                    if (start > 0L)
                    {
                        reader.ReadLine();
                    }

                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        yield return line;
                    }
                }
            }
        }

        private static string GetEditorLogPath()
        {
            if (!string.IsNullOrEmpty(Application.consoleLogPath))
            {
                return Application.consoleLogPath;
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            return string.IsNullOrEmpty(home)
                ? string.Empty
                : Path.Combine(home, "Library/Logs/Unity/Editor.log");
        }

        private static bool IsIgnoredEditorLogAssetPath(string assetPath)
        {
            return assetPath.StartsWith("Assets/DependencyAnalyzer/", StringComparison.OrdinalIgnoreCase)
                || assetPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase);
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

    }
}
