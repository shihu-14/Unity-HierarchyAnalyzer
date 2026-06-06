using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Settings;

namespace DependencyAnalyzer.Editor.Scanners
{
    public sealed class ScannerOrchestrator
    {
        private readonly SerializedPropertyScanner sceneScanner = new SerializedPropertyScanner();
        private readonly List<IDependencyScanner> extensionScanners = new List<IDependencyScanner>();

        public ScannerOrchestrator()
        {
        }

        public void RegisterScanner(IDependencyScanner scanner)
        {
            if (scanner == null)
            {
                throw new ArgumentNullException(nameof(scanner));
            }

            extensionScanners.Add(scanner);
        }

        public async Task<DependencyGraphData> ScanAsync(
            AnalyzerSettings settings,
            DependencyCache cache,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken)
        {
            var mergedGraph = new DependencyGraphData();
            var sceneGraph = await RunScannerAsync(sceneScanner, settings, cache, progress, cancellationToken);
            mergedGraph.MergeFrom(sceneGraph);

            for (var i = 0; i < extensionScanners.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var graph = await RunScannerAsync(extensionScanners[i], settings, cache, progress, cancellationToken);
                mergedGraph.MergeFrom(graph);
            }

            mergedGraph.RecalculateReferenceCounts();
            AddIssueNodes(mergedGraph, cache);
            mergedGraph.RecalculateReferenceCounts();
            progress?.Report(new ScanProgress("Dependency Analyzer", "Completed", 1, 1));
            return mergedGraph;
        }

        private static async Task<DependencyGraphData> RunScannerAsync(
            IDependencyScanner scanner,
            AnalyzerSettings settings,
            DependencyCache cache,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(new ScanProgress(scanner.Name, "Starting", 0, 1));
            try
            {
                return await scanner.ScanAsync(settings, cache, progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                var graph = new DependencyGraphData();
                graph.AddIssue(new DependencyScanIssueData(
                    scanner.Name,
                    scanner.Name,
                    "Scanner failed: " + exception.Message,
                    DependencyScanIssueSeverity.Error));
                return graph;
            }
        }

        private static void AddIssueNodes(DependencyGraphData graph, DependencyCache cache)
        {
            if (graph == null || graph.Issues.Count == 0)
            {
                return;
            }

            for (var i = 0; i < graph.Issues.Count; i++)
            {
                var issue = graph.Issues[i];
                var sourceNodeId = FindIssueSourceNodeId(graph, issue.SubjectPath);
                var issueNode = AssetScanner.CreateIssueNode(issue, cache);
                graph.AddOrUpdateNode(issueNode);

                if (string.IsNullOrEmpty(sourceNodeId))
                {
                    continue;
                }

                graph.AddEdge(new DependencyEdgeData(
                    sourceNodeId,
                    issueNode.Id,
                    issue.Severity + " Issue",
                    DependencyReferenceKind.Issue));
            }
        }

        private static string FindIssueSourceNodeId(DependencyGraphData graph, string subjectPath)
        {
            if (graph == null || string.IsNullOrEmpty(subjectPath))
            {
                return string.Empty;
            }

            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                if (string.Equals(node.Id, subjectPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.Path, subjectPath, StringComparison.OrdinalIgnoreCase))
                {
                    return node.Id;
                }
            }

            var bestNodeId = string.Empty;
            var bestLength = -1;
            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                if (string.IsNullOrEmpty(node.Path))
                {
                    continue;
                }

                var matches = subjectPath.IndexOf(node.Path, StringComparison.OrdinalIgnoreCase) >= 0
                    || node.Path.IndexOf(subjectPath, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matches || node.Path.Length <= bestLength)
                {
                    continue;
                }

                bestLength = node.Path.Length;
                bestNodeId = node.Id;
            }

            return bestNodeId;
        }
    }
}
