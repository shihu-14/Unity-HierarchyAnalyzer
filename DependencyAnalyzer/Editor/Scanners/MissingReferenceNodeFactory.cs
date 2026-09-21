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
            string typeName = "Unknown Reference",
            string namespaceQualifiedTypeName = "Unknown Reference",
            string iconContentName = "DefaultAsset Icon",
            DependencyNodeKind kind = DependencyNodeKind.MissingReference,
            MissingTargetKind missingTargetKind = MissingTargetKind.BrokenReference)
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
            node.MarkAsMissingTarget(missingTargetKind);
            return node;
        }
    }
}
