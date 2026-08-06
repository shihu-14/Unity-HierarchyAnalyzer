using System;
using System.Threading;
using System.Threading.Tasks;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Settings;

namespace DependencyAnalyzer.Editor.Scanners
{
    public interface IDependencyScanner
    {
        string Name { get; }

        Task<DependencyGraph> ScanAsync(
            AnalyzerSettings settings,
            DependencyNodeCache cache,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken);
    }
}
