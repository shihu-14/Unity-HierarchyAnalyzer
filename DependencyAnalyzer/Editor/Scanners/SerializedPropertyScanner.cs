using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DependencyAnalyzer.Editor.Scanners
{
    public sealed partial class SerializedPropertyScanner : IDependencyScanner
    {
        private const string ScannerName = "Serialized Property Scanner";
        private const string DebugErrorObjectPrefix = "Issue_Error_";

        public string Name => ScannerName;

        public async Task<DependencyGraphData> ScanAsync(
            AnalyzerSettings settings,
            DependencyCache cache,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken)
        {
            var graph = new DependencyGraphData();
            var components = CollectOpenSceneComponents(graph, cache, settings);
            var batchSize = settings.ScanYieldBatchSize;

            for (var i = 0; i < components.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                progress?.Report(new ScanProgress(Name, component.GetType().Name, i + 1, components.Count));
                ScanComponent(component, graph, cache, settings);

                if (i % batchSize == 0)
                {
                    await Task.Yield();
                }
            }

            graph.RecalculateReferenceCounts();
            return graph;
        }

        private static void ScanComponent(
            Component component,
            DependencyGraphData graph,
            DependencyCache cache,
            AnalyzerSettings settings)
        {
            var sourceNode = CreateSceneObjectNode(component, cache);
            graph.AddOrUpdateNode(sourceNode);

            SerializedObject serializedObject;
            try
            {
                serializedObject = new SerializedObject(component);
            }
            catch (Exception exception)
            {
                graph.AddIssue(new DependencyScanIssueData(
                    ScannerName,
                    sourceNode.Path,
                    "Failed to inspect component " + component.GetType().FullName + ": " + exception.Message,
                    DependencyScanIssueSeverity.Warning));
                return;
            }

            var property = serializedObject.GetIterator();
            while (property.NextVisible(true))
            {
                if (!ShouldScanInspectorObjectReference(component, property))
                {
                    continue;
                }

                var referencedObject = property.objectReferenceValue;
                if (referencedObject != null)
                {
                    if (referencedObject is MonoScript)
                    {
                        continue;
                    }

                    var targetNode = CreateObjectReferenceNode(referencedObject, cache, settings);
                    if (targetNode == null)
                    {
                        continue;
                    }

                    graph.AddOrUpdateNode(targetNode);
                    graph.AddEdge(new DependencyEdgeData(
                        sourceNode.Id,
                        targetNode.Id,
                        property.propertyPath,
                        DependencyReferenceKind.SerializedProperty));
                    continue;
                }

                if (property.objectReferenceInstanceIDValue != 0)
                {
                    var missingReferenceType = GetMissingReferenceTypeName(property);
                    var missingNode = AssetScanner.CreateMissingNode(
                        "missing:property:" + sourceNode.Id + ":" + property.propertyPath + ":" + property.objectReferenceInstanceIDValue,
                        sourceNode.Path,
                        property.propertyPath,
                        missingReferenceType,
                        GetMissingReferenceNamespaceQualifiedTypeName(missingReferenceType),
                        GetMissingReferenceIconContentName(missingReferenceType),
                        GetMissingReferenceKind(missingReferenceType));
                    sourceNode.MarkMissingReferences();
                    graph.AddOrUpdateNode(missingNode);
                    graph.AddEdge(new DependencyEdgeData(
                        sourceNode.Id,
                        missingNode.Id,
                        property.propertyPath,
                        DependencyReferenceKind.SerializedProperty,
                        true));
                }
            }
        }

    }
}
