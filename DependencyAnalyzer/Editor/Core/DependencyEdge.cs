using System;

namespace DependencyAnalyzer.Editor.Core
{
    public enum DependencyReferenceKind
    {
        Hierarchy,
        Component,
        PrefabInstance,
        StaticAsset,
        SerializedProperty,
        Issue
    }

    [Serializable]
    public sealed class DependencyEdge
    {
        public DependencyEdge(
            string sourceNodeId,
            string targetNodeId,
            string memberName,
            DependencyReferenceKind referenceKind,
            bool pointsToMissingReference = false)
        {
            SourceNodeId = sourceNodeId ?? string.Empty;
            TargetNodeId = targetNodeId ?? string.Empty;
            MemberName = memberName ?? string.Empty;
            ReferenceKind = referenceKind;
            PointsToMissingReference = pointsToMissingReference;
        }

        public string SourceNodeId { get; }
        public string TargetNodeId { get; }
        public string MemberName { get; }
        public DependencyReferenceKind ReferenceKind { get; }
        public bool PointsToMissingReference { get; }

        public string StableKey => SourceNodeId + "\u001f" + TargetNodeId + "\u001f" + MemberName + "\u001f" + ReferenceKind;
    }
}
