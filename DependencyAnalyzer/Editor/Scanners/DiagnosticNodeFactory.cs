using System;
using DependencyAnalyzer.Editor.Core;

namespace DependencyAnalyzer.Editor.Scanners
{
    public static class DiagnosticNodeFactory
    {
        internal static DependencyNode CreateMissingNode(
            string id,
            string path,
            string displayName,
            string typeName = "Missing",
            string namespaceQualifiedTypeName = "Missing Reference",
            string iconContentName = "console.warnicon.sml",
            DependencyNodeKind kind = DependencyNodeKind.MissingReference)
        {
            var node = new DependencyNode(
                id,
                default,
                path,
                displayName,
                typeName,
                namespaceQualifiedTypeName,
                Array.Empty<string>(),
                iconContentName,
                kind,
                0,
                DependencyScanIssueSeverity.Warning,
                "Missing reference");
            node.MarkMissingReferences();
            return node;
        }

        internal static DependencyNode CreateIssueNode(
            DependencyScanIssue issue,
            DependencyNodeCache cache,
            DependencyNode sourceNode)
        {
            var severity = issue == null ? DependencyScanIssueSeverity.Warning : issue.Severity;
            var subjectPath = issue == null ? string.Empty : issue.SubjectPath;
            var message = issue == null ? string.Empty : issue.Message;
            var scannerName = issue == null ? string.Empty : issue.ScannerName;
            if (sourceNode != null)
            {
                var sourceIssueNode = new DependencyNode(
                    "issue:" + severity + ":" + GetStableHash(scannerName + "\n" + subjectPath + "\n" + message),
                    sourceNode.GlobalObjectId,
                    string.IsNullOrEmpty(subjectPath) ? sourceNode.Path : subjectPath,
                    sourceNode.DisplayName,
                    sourceNode.TypeName,
                    sourceNode.NamespaceQualifiedTypeName,
                    sourceNode.AssetLabels,
                    sourceNode.IconContentName,
                    sourceNode.Kind,
                    sourceNode.InstanceId,
                    severity,
                    message);
                return cache.Store(sourceIssueNode);
            }

            var node = new DependencyNode(
                "issue:" + severity + ":" + GetStableHash(scannerName + "\n" + subjectPath + "\n" + message),
                default,
                subjectPath,
                severity + ": " + (string.IsNullOrEmpty(message) ? "Issue" : message),
                severity + " Issue",
                "DependencyAnalyzer.Issue",
                Array.Empty<string>(),
                GetIssueIconContentName(severity),
                DependencyNodeKind.Issue,
                0,
                severity,
                message);
            return cache.Store(node);
        }

        private static string GetIssueIconContentName(DependencyScanIssueSeverity severity)
        {
            switch (severity)
            {
                case DependencyScanIssueSeverity.Error:
                    return "console.erroricon.sml";
                case DependencyScanIssueSeverity.Info:
                    return "console.infoicon.sml";
                default:
                    return "console.warnicon.sml";
            }
        }

        private static string GetStableHash(string value)
        {
            unchecked
            {
                const uint offset = 2166136261;
                const uint prime = 16777619;
                var hash = offset;
                if (!string.IsNullOrEmpty(value))
                {
                    for (var i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= prime;
                    }
                }

                return hash.ToString("x8");
            }
        }
    }
}
