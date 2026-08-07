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

        public async Task<DependencyGraph> ScanAsync(
            AnalyzerSettings settings,
            DependencyNodeCache cache,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken)
        {
            var mergedGraph = new DependencyGraph();
            var sceneGraph = await RunScannerAsync(sceneScanner, settings, cache, progress, cancellationToken);
            mergedGraph.MergeFrom(sceneGraph);

            for (var i = 0; i < extensionScanners.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var graph = await RunScannerAsync(extensionScanners[i], settings, cache, progress, cancellationToken);
                mergedGraph.MergeFrom(graph);
            }

            mergedGraph.RecalculateReferenceCounts();
            AnalyzerDiagnosticReporter.Report(mergedGraph.Issues);
            progress?.Report(new ScanProgress("Dependency Analyzer", "Completed", 1, 1));
            return mergedGraph;
        }

        private static async Task<DependencyGraph> RunScannerAsync(
            IDependencyScanner scanner,
            AnalyzerSettings settings,
            DependencyNodeCache cache,
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
                var graph = new DependencyGraph();
                graph.AddIssue(new DependencyScanIssue(
                    scanner.Name,
                    scanner.Name,
                    "Scanner failed: " + exception.Message,
                    DependencyScanIssueSeverity.Error));
                return graph;
            }
        }
    }
}
