namespace DependencyAnalyzer.Editor.Scanners.Issues
{
    internal readonly struct ConsoleLogEntry
    {
        public ConsoleLogEntry(string condition, string file, string stackTrace, int line, int mode, int instanceId)
        {
            Condition = condition ?? string.Empty;
            File = file ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            Line = line;
            Mode = mode;
            InstanceId = instanceId;
        }

        public string Condition { get; }
        public string File { get; }
        public string StackTrace { get; }
        public int Line { get; }
        public int Mode { get; }
        public int InstanceId { get; }
    }
}
