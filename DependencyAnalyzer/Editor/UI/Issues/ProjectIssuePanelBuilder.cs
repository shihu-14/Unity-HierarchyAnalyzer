using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.GraphView;
using DependencyAnalyzer.Editor.UI.Icons;
using UnityEngine;

namespace DependencyAnalyzer.Editor.UI.Issues
{
    internal static class ProjectIssuePanelBuilder
    {
        public static ProjectIssuePanelModel Build(DependencyGraph graph)
        {
            if (graph == null)
            {
                return new ProjectIssuePanelModel(null);
            }

            var groupsByType = new Dictionary<string, ObjectGroupAccumulator>(StringComparer.OrdinalIgnoreCase);

            foreach (var edge in graph.Edges.Where(edge => edge != null && edge.PointsToMissingReference))
            {
                graph.TryGetNode(edge.SourceNodeId, out var sourceNode);
                graph.TryGetNode(edge.TargetNodeId, out var missingNode);
                var objectType = GetCanonicalObjectType(edge, missingNode);
                if (!groupsByType.TryGetValue(objectType, out var accumulator))
                {
                    accumulator = new ObjectGroupAccumulator(
                        objectType,
                        DependencyIconProvider.GetIcon(missingNode),
                        DependencyNodeStyleResolver.GetTypeAccentColor(objectType));
                    groupsByType.Add(objectType, accumulator);
                }

                accumulator.Locations.Add(BuildLocation(sourceNode, edge.MemberName));
            }

            var groups = groupsByType.Values
                .OrderBy(group => group.ObjectType, StringComparer.OrdinalIgnoreCase)
                .Select(group => new ProjectIssueGroup(
                    "issue:type:" + NormalizeIdSegment(group.ObjectType),
                    group.ObjectType,
                    group.Icon,
                    group.AccentColor,
                    group.Locations))
                .ToList();

            return new ProjectIssuePanelModel(groups);
        }

        private static string GetCanonicalObjectType(DependencyEdge edge, DependencyNode missingNode)
        {
            if (missingNode != null
                && (missingNode.MissingTargetState == MissingTargetKind.MissingScript
                    || IsLegacyMissingScript(edge, missingNode)))
            {
                return "Script";
            }

            return NormalizeObjectType(
                missingNode == null ? string.Empty : missingNode.TypeName,
                missingNode == null ? string.Empty : missingNode.NamespaceQualifiedTypeName);
        }

        private static bool IsLegacyMissingScript(DependencyEdge edge, DependencyNode missingNode)
        {
            return edge.ReferenceKind == DependencyReferenceKind.Component
                && missingNode.Kind == DependencyNodeKind.Component
                && string.Equals(missingNode.TypeName, "Script", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeObjectType(string typeName, string namespaceQualifiedTypeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)
                || string.Equals(typeName, "Unknown", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "Missing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "Missing Reference", StringComparison.OrdinalIgnoreCase))
            {
                return "Unknown Reference";
            }

            typeName = typeName.Trim();
            if (string.Equals(typeName, "Object", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(
                    namespaceQualifiedTypeName,
                    "UnityEngine.GameObject",
                    StringComparison.OrdinalIgnoreCase)
                    ? "GameObject"
                    : "Object Reference";
            }

            if (string.Equals(typeName, "Object Reference", StringComparison.OrdinalIgnoreCase))
            {
                return "Object Reference";
            }

            if (string.Equals(typeName, "GameObject", StringComparison.OrdinalIgnoreCase)
                || string.Equals(namespaceQualifiedTypeName, "UnityEngine.GameObject", StringComparison.OrdinalIgnoreCase))
            {
                return "GameObject";
            }

            if (string.Equals(typeName, "Texture2D", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "Texture3D", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "Cubemap", StringComparison.OrdinalIgnoreCase))
            {
                return "Texture";
            }

            if (string.Equals(typeName, "RuntimeAnimatorController", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "AnimatorController", StringComparison.OrdinalIgnoreCase))
            {
                return "Animator";
            }

            if (string.Equals(typeName, "MonoScript", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "MonoBehaviour", StringComparison.OrdinalIgnoreCase)
                || typeName.EndsWith("Script", StringComparison.OrdinalIgnoreCase))
            {
                return "Script";
            }

            return typeName;
        }

        private static string NormalizeIdSegment(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "object"
                : value.Trim().ToLowerInvariant().Replace(' ', '-');
        }

        private static ProjectIssueLocation BuildLocation(
            DependencyNode sourceNode,
            string memberName)
        {
            if (sourceNode == null)
            {
                return new ProjectIssueLocation(
                    null,
                    "No related node",
                    "No related node",
                    string.Empty);
            }

            var path = (sourceNode.Path ?? string.Empty).Replace('\\', '/');
            var segments = new List<string>();
            var sceneSeparator = path.IndexOf("::", StringComparison.Ordinal);
            if (sceneSeparator >= 0)
            {
                var scenePath = path.Substring(0, sceneSeparator);
                segments.Add(GetSceneDisplayName(scenePath));
                segments.AddRange(SplitPath(path.Substring(sceneSeparator + 2)));
            }
            else
            {
                segments.AddRange(SplitPath(path));
            }

            var label = string.IsNullOrWhiteSpace(memberName)
                ? sourceNode.DisplayName
                : memberName;
            if (string.IsNullOrWhiteSpace(memberName) && segments.Count > 0)
            {
                label = segments[segments.Count - 1];
                segments.RemoveAt(segments.Count - 1);
            }

            return new ProjectIssueLocation(
                segments,
                label,
                sourceNode.DisplayName,
                sourceNode.Id);
        }

        private static List<string> SplitPath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? new List<string>()
                : path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private static string GetSceneDisplayName(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                return "Unsaved Scene";
            }

            var displayName = Path.GetFileNameWithoutExtension(scenePath);
            return string.IsNullOrEmpty(displayName) ? scenePath : displayName;
        }

        private sealed class ObjectGroupAccumulator
        {
            public ObjectGroupAccumulator(string objectType, Texture icon, Color accentColor)
            {
                ObjectType = objectType;
                Icon = icon;
                AccentColor = accentColor;
            }

            public string ObjectType { get; }
            public Texture Icon { get; }
            public Color AccentColor { get; }
            public List<ProjectIssueLocation> Locations { get; } = new List<ProjectIssueLocation>();
        }
    }
}
