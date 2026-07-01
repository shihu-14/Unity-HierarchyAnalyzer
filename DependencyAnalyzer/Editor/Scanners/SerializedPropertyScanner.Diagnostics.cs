using System;
using System.Collections.Generic;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DependencyAnalyzer.Editor.Scanners
{
    public sealed partial class SerializedPropertyScanner
    {
        private static void AddComponentHealthIssues(
            Component component,
            DependencyNodeData ownerNode,
            DependencyGraphData graph)
        {
            if (component == null || ownerNode == null || graph == null)
            {
                return;
            }

            var subjectPath = ownerNode.Path;
            var meshFilter = component as MeshFilter;
            if (meshFilter != null && meshFilter.sharedMesh == null)
            {
                AddComponentIssue(graph, subjectPath, "MeshFilter has no shared mesh", DependencyScanIssueSeverity.Warning);
                return;
            }

            var skinnedMeshRenderer = component as SkinnedMeshRenderer;
            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh == null)
            {
                AddComponentIssue(graph, subjectPath, "SkinnedMeshRenderer has no shared mesh", DependencyScanIssueSeverity.Warning);
            }

            var renderer = component as Renderer;
            if (renderer != null)
            {
                var materials = renderer.sharedMaterials;
                if (materials != null)
                {
                    for (var i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null)
                        {
                            AddComponentIssue(graph, subjectPath, renderer.GetType().Name + " has an empty material slot [" + i + "]", DependencyScanIssueSeverity.Warning);
                        }
                    }
                }
            }

            var audioSource = component as AudioSource;
            if (audioSource != null && audioSource.playOnAwake && audioSource.clip == null)
            {
                AddComponentIssue(graph, subjectPath, "AudioSource is play-on-awake but has no clip", DependencyScanIssueSeverity.Warning);
            }

            var animator = component as Animator;
            if (animator != null && animator.runtimeAnimatorController == null)
            {
                AddComponentIssue(graph, subjectPath, "Animator has no controller", DependencyScanIssueSeverity.Warning);
            }
        }

        private static void AddGameObjectHealthIssues(
            GameObject gameObject,
            DependencyNodeData ownerNode,
            DependencyGraphData graph)
        {
            if (gameObject == null || ownerNode == null || graph == null)
            {
                return;
            }

            if (!gameObject.name.StartsWith(DebugErrorObjectPrefix, StringComparison.Ordinal))
            {
                return;
            }

            AddComponentIssue(
                graph,
                ownerNode.Path,
                "Debug error marker: " + FormatDebugIssueName(gameObject.name.Substring(DebugErrorObjectPrefix.Length)),
                DependencyScanIssueSeverity.Error);
        }

        private static string FormatDebugIssueName(string value)
        {
            return string.IsNullOrEmpty(value)
                ? "Object"
                : value.Replace('_', ' ');
        }

        private static void AddComponentIssue(
            DependencyGraphData graph,
            string subjectPath,
            string message,
            DependencyScanIssueSeverity severity)
        {
            graph.AddIssue(new DependencyScanIssueData(
                ScannerName,
                subjectPath,
                message,
                severity));
        }

    }
}
