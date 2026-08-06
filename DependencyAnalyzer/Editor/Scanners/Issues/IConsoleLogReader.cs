using System;
using System.Collections.Generic;

namespace DependencyAnalyzer.Editor.Scanners.Issues
{
    internal interface IConsoleLogReader
    {
        ConsoleLogReadResult Read();
    }

    internal sealed class ConsoleLogReadResult
    {
        private ConsoleLogReadResult(bool succeeded, IReadOnlyList<ConsoleLogEntry> entries, string errorMessage)
        {
            Succeeded = succeeded;
            Entries = entries ?? Array.Empty<ConsoleLogEntry>();
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool Succeeded { get; }
        public IReadOnlyList<ConsoleLogEntry> Entries { get; }
        public string ErrorMessage { get; }

        public static ConsoleLogReadResult Success(IReadOnlyList<ConsoleLogEntry> entries)
        {
            return new ConsoleLogReadResult(true, entries, string.Empty);
        }

        public static ConsoleLogReadResult Failure(string errorMessage)
        {
            return new ConsoleLogReadResult(false, Array.Empty<ConsoleLogEntry>(), errorMessage);
        }
    }
}
