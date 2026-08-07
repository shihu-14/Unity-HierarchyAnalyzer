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
        private const string MissingScriptGroupId = "issue:missing-script";
        private const string BrokenReferenceGroupId = "issue:broken-missing-reference";

        public static ProjectIssuePanelModel Build(DependencyGraph graph)
        {
            if (graph == null)
            {
                return new ProjectIssuePanelModel(null);
            }

            var missingScripts = new List<ProjectIssueLocation>();
            var brokenReferences = new Dictionary<string, ObjectGroupAccumulator>(StringComparer.OrdinalIgnoreCase);

            foreach (var edge in graph.Edges.Where(edge => edge != null && edge.PointsToMissingReference))
            {
                graph.TryGetNode(edge.SourceNodeId, out var sourceNode);
                graph.TryGetNode(edge.TargetNodeId, out var missingNode);
                var location = BuildLocation(sourceNode, edge.MemberName);
                if (IsMissingScript(edge, missingNode))
                {
                    missingScripts.Add(location);
                    continue;
                }

                var objectType = NormalizeObjectType(missingNode == null ? string.Empty : missingNode.TypeName);
                if (!brokenReferences.TryGetValue(objectType, out var accumulator))
                {
                    accumulator = new ObjectGroupAccumulator(
                        objectType,
                        missingNode == null ? null : DependencyIconProvider.GetIcon(missingNode));
                    brokenReferences.Add(objectType, accumulator);
                }

                accumulator.Locations.Add(location);
            }

            var groups = new List<ProjectIssueGroup>();
            if (missingScripts.Count > 0)
            {
                groups.Add(new ProjectIssueGroup(
                    MissingScriptGroupId,
                    ProjectIssueType.MissingScript,
                    "Missing Script",
                    missingScripts.OrderBy(location => location.SortKey, StringComparer.OrdinalIgnoreCase),
                    null));
            }

            if (brokenReferences.Count > 0)
            {
                var objectGroups = brokenReferences.Values
                    .OrderBy(group => group.ObjectType, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new ProjectIssueObjectGroup(
                        BrokenReferenceGroupId + ":type:" + NormalizeIdSegment(group.ObjectType),
                        group.ObjectType,
                        group.Icon,
                        group.Locations))
                    .ToList();
                groups.Add(new ProjectIssueGroup(
                    BrokenReferenceGroupId,
                    ProjectIssueType.BrokenMissingReference,
                    "Broken Missing Reference",
                    null,
                    objectGroups));
            }

            return new ProjectIssuePanelModel(groups);
        }

        private static bool IsMissingScript(DependencyEdge edge, DependencyNode missingNode)
        {
            return edge.ReferenceKind == DependencyReferenceKind.Component
                && missingNode != null
                && missingNode.Kind == DependencyNodeKind.Component
                && string.Equals(missingNode.TypeName, "Script", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeObjectType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)
                || string.Equals(typeName, "Object", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "Missing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "Missing Reference", StringComparison.OrdinalIgnoreCase))
            {
                return "Object Reference";
            }

            typeName = typeName.Trim();
            if (string.Equals(typeName, "Texture2D", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "Texture3D", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "Cubemap", StringComparison.OrdinalIgnoreCase))
            {
                return "Texture";
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
                ? "object-reference"
                : value.Trim().ToLowerInvariant().Replace(' ', '-');
        }

        private static ProjectIssueLocation BuildLocation(DependencyNode sourceNode, string memberName)
        {
            if (sourceNode == null)
            {
                return new ProjectIssueLocation(
                    null,
                    "No related node",
                    string.Empty,
                    new Color(0.38f, 0.42f, 0.47f));
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
                sourceNode.Id,
                DependencyNodeStyleResolver.GetNodeAccentColor(sourceNode));
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
            public ObjectGroupAccumulator(string objectType, Texture icon)
            {
                ObjectType = objectType;
                Icon = icon;
            }

            public string ObjectType { get; }
            public Texture Icon { get; }
            public List<ProjectIssueLocation> Locations { get; } = new List<ProjectIssueLocation>();
        }
    }
}
