using System;
using System.Collections.Generic;
using UnityEditor;

namespace DependencyAnalyzer.Editor.Core
{
    internal enum MissingTargetKind
    {
        None,
        MissingScript,
        BrokenReference
    }

    public enum DependencyNodeKind
    {
        Asset,
        SceneObject,
        Component,
        MissingReference,
        Issue
    }

    [Serializable]
    public sealed class DependencyNode
    {
        private readonly List<string> assetLabels;

        public DependencyNode(
            string id,
            GlobalObjectId globalObjectId,
            string path,
            string displayName,
            string typeName,
            string namespaceQualifiedTypeName,
            IEnumerable<string> labels,
            string iconContentName,
            DependencyNodeKind kind,
            int instanceId = 0,
            DependencyScanIssueSeverity? issueSeverity = null,
            string issueMessage = null)
        {
            Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id;
            GlobalObjectId = globalObjectId;
            Path = path ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(displayName) ? "(Unnamed)" : displayName;
            TypeName = string.IsNullOrEmpty(typeName) ? "Unknown" : typeName;
            NamespaceQualifiedTypeName = string.IsNullOrEmpty(namespaceQualifiedTypeName) ? TypeName : namespaceQualifiedTypeName;
            assetLabels = labels == null ? new List<string>() : new List<string>(labels);
            IconContentName = string.IsNullOrEmpty(iconContentName) ? "DefaultAsset Icon" : iconContentName;
            Kind = kind;
            InstanceId = instanceId;
            IssueSeverity = issueSeverity;
            IssueMessage = issueMessage ?? string.Empty;
        }

        public string Id { get; }
        public GlobalObjectId GlobalObjectId { get; }
        public string Path { get; }
        public string DisplayName { get; }
        public string TypeName { get; }
        public string NamespaceQualifiedTypeName { get; }
        public IReadOnlyList<string> AssetLabels => assetLabels;
        public string IconContentName { get; }
        public DependencyNodeKind Kind { get; }
        public int InstanceId { get; }
        public DependencyScanIssueSeverity? IssueSeverity { get; }
        public string IssueMessage { get; }
        public bool HasMissingReferences { get; private set; }
        internal MissingTargetKind MissingTargetState { get; private set; }
        internal bool IsMissingTarget => MissingTargetState != MissingTargetKind.None
            || Kind == DependencyNodeKind.MissingReference;
        public int DependencyCount { get; private set; }
        public int UsedByCount { get; private set; }
        public bool HasIssue => IssueSeverity.HasValue;

        public bool IsHeavyLeafType
        {
            get
            {
                return TypeName == "AudioClip"
                    || TypeName == "Texture2D"
                    || TypeName == "Texture"
                    || TypeName == "Mesh"
                    || NamespaceQualifiedTypeName == "UnityEngine.AudioClip"
                    || NamespaceQualifiedTypeName == "UnityEngine.Texture2D"
                    || NamespaceQualifiedTypeName == "UnityEngine.Texture"
                    || NamespaceQualifiedTypeName == "UnityEngine.Mesh";
            }
        }

        public string LabelsText => assetLabels.Count == 0 ? "(none)" : string.Join(", ", assetLabels);

        public void MarkMissingReferences()
        {
            HasMissingReferences = true;
        }

        internal void MarkAsMissingTarget(MissingTargetKind missingTargetKind)
        {
            if (missingTargetKind == MissingTargetKind.None)
            {
                return;
            }

            MissingTargetState = missingTargetKind;
            MarkMissingReferences();
        }

        public void SetReferenceCounts(int dependencyCount, int usedByCount)
        {
            DependencyCount = Math.Max(0, dependencyCount);
            UsedByCount = Math.Max(0, usedByCount);
        }
    }
}
