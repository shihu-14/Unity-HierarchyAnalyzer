using System;
using System.Threading;
using System.Threading.Tasks;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Settings;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Scanners
{
    public sealed partial class SerializedPropertyScanner : IDependencyScanner
    {
        private const string ScannerName = "Serialized Property Scanner";
        private readonly ISerializedObjectReferenceReader referenceReader;

        public SerializedPropertyScanner()
            : this(new UnitySerializedObjectReferenceReader())
        {
        }

        internal SerializedPropertyScanner(ISerializedObjectReferenceReader referenceReader)
        {
            this.referenceReader = referenceReader ?? throw new ArgumentNullException(nameof(referenceReader));
        }

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
                try
                {
                    ScanComponent(component, graph, cache, settings);
                }
                catch (Exception exception)
                {
                    graph.AddIssue(new DependencyScanIssueData(
                        ScannerName,
                        GetSafeObjectPath(component),
                        "Failed to inspect component " + GetSafeComponentTypeName(component) + ": " + exception.Message,
                        DependencyScanIssueSeverity.Warning));
                }

                if ((i + 1) % batchSize == 0)
                {
                    await Task.Yield();
                }
            }

            graph.RecalculateReferenceCounts();
            return graph;
        }

        private void ScanComponent(
            Component component,
            DependencyGraphData graph,
            DependencyCache cache,
            AnalyzerSettings settings)
        {
            var isVisibleComponent = ShouldVisualizeComponent(component);
            var sourceObject = isVisibleComponent ? (UnityEngine.Object)component : component.gameObject;
            var sourceNode = CreateSceneObjectNode(sourceObject, cache);
            graph.AddOrUpdateNode(sourceNode);
            var memberPrefix = isVisibleComponent ? string.Empty : GetHiddenComponentMemberPrefix(component);

            try
            {
                foreach (var reference in referenceReader.Read(component))
                {
                    ScanObjectReference(reference, sourceNode, memberPrefix, graph, cache, settings);
                }
            }
            catch (Exception exception)
            {
                graph.AddIssue(new DependencyScanIssueData(
                    ScannerName,
                    sourceNode.Path,
                    "Failed to inspect component " + component.GetType().FullName + ": " + exception.Message,
                    DependencyScanIssueSeverity.Warning));
            }
        }

        private static void ScanObjectReference(
            SerializedObjectReferenceInfo reference,
            DependencyNodeData sourceNode,
            string memberPrefix,
            DependencyGraphData graph,
            DependencyCache cache,
            AnalyzerSettings settings)
        {
            var memberName = memberPrefix + reference.PropertyPath;
            if (reference.State == SerializedObjectReferenceState.None)
            {
                return;
            }

            if (reference.State == SerializedObjectReferenceState.Unreadable)
            {
                graph.AddIssue(new DependencyScanIssueData(
                    ScannerName,
                    sourceNode.Path,
                    "Failed to read " + memberName + ": " + reference.ErrorMessage,
                    DependencyScanIssueSeverity.Warning));
                return;
            }

            if (reference.State == SerializedObjectReferenceState.Valid)
            {
                if (reference.ReferencedObject is UnityEditor.MonoScript)
                {
                    return;
                }

                try
                {
                    var targetNode = CreateObjectReferenceNode(reference.ReferencedObject, cache, settings);
                    if (targetNode == null)
                    {
                        return;
                    }

                    graph.AddOrUpdateNode(targetNode);
                    graph.AddEdge(new DependencyEdgeData(
                        sourceNode.Id,
                        targetNode.Id,
                        memberName,
                        DependencyReferenceKind.SerializedProperty));
                }
                catch (Exception exception)
                {
                    graph.AddIssue(new DependencyScanIssueData(
                        ScannerName,
                        sourceNode.Path,
                        "Failed to resolve " + memberName + ": " + exception.Message,
                        DependencyScanIssueSeverity.Warning));
                }

                return;
            }

            var missingReferenceType = GetMissingReferenceTypeName(reference.SerializedTypeName);
            var missingNode = AssetScanner.CreateMissingNode(
                "missing:property:" + sourceNode.Id + ":" + memberName + ":" + reference.MissingInstanceId,
                sourceNode.Path,
                memberName,
                missingReferenceType,
                GetMissingReferenceNamespaceQualifiedTypeName(missingReferenceType),
                GetMissingReferenceIconContentName(missingReferenceType),
                GetMissingReferenceKind(missingReferenceType));
            sourceNode.MarkMissingReferences();
            graph.AddOrUpdateNode(missingNode);
            graph.AddEdge(new DependencyEdgeData(
                sourceNode.Id,
                missingNode.Id,
                memberName,
                DependencyReferenceKind.SerializedProperty,
                true));
        }

        private static string GetHiddenComponentMemberPrefix(Component component)
        {
            var typeName = GetSafeComponentTypeName(component);
            var sameTypeComponents = component.gameObject.GetComponents(component.GetType());
            if (sameTypeComponents.Length <= 1)
            {
                return typeName + ".";
            }

            for (var i = 0; i < sameTypeComponents.Length; i++)
            {
                if (sameTypeComponents[i] == component)
                {
                    return typeName + "[" + i + "].";
                }
            }

            return typeName + ".";
        }

        private static string GetSafeComponentTypeName(Component component)
        {
            try
            {
                return component == null ? "Unknown Component" : component.GetType().FullName;
            }
            catch (Exception)
            {
                return "Unknown Component";
            }
        }

        private static string GetSafeObjectPath(UnityEngine.Object unityObject)
        {
            try
            {
                return unityObject == null ? string.Empty : GetObjectPath(unityObject);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
