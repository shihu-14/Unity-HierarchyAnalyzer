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
}
