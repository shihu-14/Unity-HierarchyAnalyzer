namespace DependencyAnalyzer.Editor.Scanners.Issues
{
    internal readonly struct ConsoleLogEntry
    {
        public ConsoleLogEntry(string condition, string file, string stackTrace, int line, int mode, int instanceId)
            : this(condition, file, stackTrace, line, 0, mode, instanceId, 0, -1, -1, 1)
        {
        }

        public ConsoleLogEntry(
            string condition,
            string file,
            string stackTrace,
            int line,
            int column,
            int mode,
            int instanceId,
            int identifier,
            int globalLineIndex,
            int rowIndex,
            int occurrenceCount)
        {
            Condition = condition ?? string.Empty;
            File = file ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            Line = line;
            Column = column;
            Mode = mode;
            InstanceId = instanceId;
            Identifier = identifier;
            GlobalLineIndex = globalLineIndex;
            RowIndex = rowIndex;
            OccurrenceCount = occurrenceCount < 1 ? 1 : occurrenceCount;
        }

        public string Condition { get; }
        public string File { get; }
        public string StackTrace { get; }
        public int Line { get; }
        public int Column { get; }
        public int Mode { get; }
        public int InstanceId { get; }
        public int Identifier { get; }
        public int GlobalLineIndex { get; }
        public int RowIndex { get; }
        public int OccurrenceCount { get; }
    }
}
