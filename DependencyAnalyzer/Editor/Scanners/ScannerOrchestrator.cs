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
        private readonly List<IDependencyScanner> scanners = new List<IDependencyScanner>();

        public ScannerOrchestrator()
        {
            RegisterScanner(new AssetScanner());
            RegisterScanner(new SerializedPropertyScanner());
        }

        public void RegisterScanner(IDependencyScanner scanner)
        {
            if (scanner == null)
            {
                throw new ArgumentNullException(nameof(scanner));
            }

            scanners.Add(scanner);
        }

        public async Task<DependencyGraphData> ScanAsync(
            AnalyzerSettings settings,
            DependencyCache cache,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken)
        {
            var mergedGraph = new DependencyGraphData();
            for (var i = 0; i < scanners.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scanner = scanners[i];
                progress?.Report(new ScanProgress(scanner.Name, "Starting", 0, 1));
                var graph = await scanner.ScanAsync(settings, cache, progress, cancellationToken);
                mergedGraph.MergeFrom(graph);
            }

            mergedGraph.RecalculateReferenceCounts();
            progress?.Report(new ScanProgress("Dependency Analyzer", "Completed", 1, 1));
            return mergedGraph;
        }
    }
}
