using System;
using DependencyAnalyzer.Editor.Core;

namespace DependencyAnalyzer.Editor.Scanners
{
    internal static class MissingReferenceNodeFactory
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
    }
}
