using System;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Scanners.Issues;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class UnityConsoleLogReaderTests
    {
        [Test]
        public void Read_GetsCurrentWarningAndErrorWithMetadata()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var warningMessage = "DependencyAnalyzer reader warning " + suffix;
            var errorMessage = "DependencyAnalyzer reader error " + suffix;
            var context = new GameObject("ConsoleReaderContext");
            try
            {
                LogAssert.Expect(LogType.Warning, warningMessage);
                Debug.LogWarning(warningMessage, context);
                LogAssert.Expect(LogType.Error, errorMessage);
                Debug.LogError(errorMessage, context);

                var result = new UnityConsoleLogReader().Read();

                Assert.IsTrue(result.Succeeded, result.ErrorMessage);
                var warning = FindEntry(result, warningMessage);
                var error = FindEntry(result, errorMessage);
                Assert.IsTrue(ConsoleIssueParser.TryGetSeverity(warning, out var warningSeverity));
                Assert.AreEqual(DependencyScanIssueSeverity.Warning, warningSeverity);
                Assert.IsTrue(ConsoleIssueParser.TryGetSeverity(error, out var errorSeverity));
                Assert.AreEqual(DependencyScanIssueSeverity.Error, errorSeverity);
                Assert.IsNotEmpty(warning.File);
                Assert.Greater(warning.Line, 0);
                Assert.AreEqual(context.GetInstanceID(), warning.InstanceId);
                Assert.IsNotEmpty(warning.StackTrace);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(context);
            }
        }

        private static ConsoleLogEntry FindEntry(ConsoleLogReadResult result, string message)
        {
            for (var i = 0; i < result.Entries.Count; i++)
            {
                if (result.Entries[i].Condition.IndexOf(message, StringComparison.Ordinal) >= 0)
                {
                    return result.Entries[i];
                }
            }

            Assert.Fail("Current Unity Console entry was not returned: " + message);
            return default;
        }
    }
}
