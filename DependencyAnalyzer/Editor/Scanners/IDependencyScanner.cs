using System.Threading;
using System.Threading.Tasks;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Settings;

namespace DependencyAnalyzer.Editor.Scanners
{
    public readonly struct ScanProgress
    {
        public ScanProgress(string scannerName, string message, int completed, int total)
        {
            ScannerName = scannerName ?? string.Empty;
            Message = message ?? string.Empty;
            Completed = completed;
            Total = total;
        }

        public string ScannerName { get; }
        public string Message { get; }
        public int Completed { get; }
        public int Total { get; }
        public float Ratio => Total <= 0 ? 0f : (float)Completed / Total;
    }

    public interface IDependencyScanner
    {
        string Name { get; }

        Task<DependencyGraphData> ScanAsync(
            AnalyzerSettings settings,
            DependencyCache cache,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken);
    }
}
