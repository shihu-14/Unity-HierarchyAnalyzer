using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Scanners.Issues
{
    internal sealed class UnityConsoleLogReader : IConsoleLogReader
    {
        public ConsoleLogReadResult Read()
        {
            try
            {
                return ConsoleLogReadResult.Success(ReadEntries());
            }
            catch (Exception exception)
            {
                return ConsoleLogReadResult.Failure(GetFailureMessage(exception));
            }
        }

        private static IReadOnlyList<ConsoleLogEntry> ReadEntries()
        {
            var editorAssembly = typeof(EditorWindow).Assembly;
            var logEntriesType = editorAssembly.GetType("UnityEditor.LogEntries");
            var logEntryType = editorAssembly.GetType("UnityEditor.LogEntry");
            if (logEntriesType == null || logEntryType == null)
            {
                throw new MissingMemberException("UnityEditor.LogEntries or UnityEditor.LogEntry was not found.");
            }

            if ((!HasMember(logEntryType, "message") && !HasMember(logEntryType, "condition"))
                || !HasMember(logEntryType, "mode"))
            {
                throw new MissingMemberException("UnityEditor.LogEntry message or mode metadata was not found.");
            }

            var start = GetRequiredMethod(logEntriesType, "StartGettingEntries", 0);
            var end = GetRequiredMethod(logEntriesType, "EndGettingEntries", 0);
            var getEntry = FindGetEntryMethod(logEntriesType);
            if (getEntry == null)
            {
                throw new MissingMethodException(logEntriesType.FullName, "GetEntryInternal");
            }

            var getEntryCount = FindMethod(logEntriesType, "GetEntryCount", 1);
            var entries = new List<ConsoleLogEntry>();
            var started = false;
            try
            {
                var countValue = start.Invoke(null, null);
                started = true;
                if (!(countValue is int count) || count < 0)
                {
                    throw new InvalidOperationException("Unity Console returned an invalid entry count.");
                }

                entries.Capacity = count;
                for (var rowIndex = 0; rowIndex < count; rowIndex++)
                {
                    var entryObject = Activator.CreateInstance(logEntryType, true);
                    var arguments = new[] { (object)rowIndex, entryObject };
                    var getEntryResult = getEntry.Invoke(null, arguments);
                    if (getEntryResult is bool succeeded && !succeeded)
                    {
                        throw new InvalidOperationException("Unity Console could not read row " + rowIndex + ".");
                    }

                    entryObject = arguments[1];
                    var message = GetStringValue(logEntryType, entryObject, "message", "condition");
                    var stackTrace = GetStringValue(logEntryType, entryObject, "stackTrace");
                    SplitMessageAndStackTrace(
                        message,
                        GetIntValue(logEntryType, entryObject, "callstackTextStartUTF16"),
                        ref stackTrace,
                        out var condition);

                    entries.Add(new ConsoleLogEntry(
                        condition,
                        GetStringValue(logEntryType, entryObject, "file"),
                        stackTrace,
                        GetIntValue(logEntryType, entryObject, "line"),
                        GetIntValue(logEntryType, entryObject, "column"),
                        GetIntValue(logEntryType, entryObject, "mode"),
                        GetContextInstanceId(logEntryType, entryObject),
                        GetIntValue(logEntryType, entryObject, "identifier"),
                        GetIntValueOrDefault(logEntryType, entryObject, -1, "globalLineIndex"),
                        rowIndex,
                        GetOccurrenceCount(getEntryCount, rowIndex)));
                }
            }
            finally
            {
                if (started)
                {
                    end.Invoke(null, null);
                }
            }

            return entries;
        }

        private static MethodInfo GetRequiredMethod(Type type, string name, int parameterCount)
        {
            var method = FindMethod(type, name, parameterCount);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }

            return method;
        }

        private static MethodInfo FindMethod(Type type, string name, int parameterCount)
        {
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name == name && method.GetParameters().Length == parameterCount)
                {
                    return method;
                }
            }

            return null;
        }

        private static MethodInfo FindGetEntryMethod(Type logEntriesType)
        {
            foreach (var method in logEntriesType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != "GetEntryInternal")
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType == typeof(int))
                {
                    return method;
                }
            }

            return null;
        }

        private static int GetOccurrenceCount(MethodInfo getEntryCount, int rowIndex)
        {
            if (getEntryCount == null)
            {
                return 1;
            }

            var value = getEntryCount.Invoke(null, new object[] { rowIndex });
            return Math.Max(1, ConvertToInt(value));
        }

        private static void SplitMessageAndStackTrace(
            string message,
            int callstackStart,
            ref string stackTrace,
            out string condition)
        {
            message = message ?? string.Empty;
            stackTrace = stackTrace ?? string.Empty;
            if (string.IsNullOrEmpty(stackTrace) && callstackStart > 0 && callstackStart <= message.Length)
            {
                stackTrace = message.Substring(callstackStart).Trim();
                condition = message.Substring(0, callstackStart).Trim();
                return;
            }

            condition = message.Trim();
            stackTrace = stackTrace.Trim();
        }

        private static int GetContextInstanceId(Type logEntryType, object entryObject)
        {
            var instanceId = GetIntValue(logEntryType, entryObject, "instanceID", "instanceId");
            if (instanceId != 0)
            {
                return instanceId;
            }

            var entityId = GetMemberValue(logEntryType, entryObject, "entityId");
            if (entityId == null)
            {
                return 0;
            }

            foreach (var method in typeof(EditorUtility).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var parameters = method.GetParameters();
                if (method.Name != "EntityIdToObject"
                    || parameters.Length != 1
                    || !parameters[0].ParameterType.IsInstanceOfType(entityId))
                {
                    continue;
                }

                var contextObject = method.Invoke(null, new[] { entityId }) as UnityEngine.Object;
                return contextObject == null ? 0 : contextObject.GetInstanceID();
            }

            return 0;
        }

        private static string GetStringValue(Type type, object instance, params string[] names)
        {
            var value = GetMemberValue(type, instance, names);
            return value as string ?? string.Empty;
        }

        private static int GetIntValue(Type type, object instance, params string[] names)
        {
            return ConvertToInt(GetMemberValue(type, instance, names));
        }

        private static int GetIntValueOrDefault(Type type, object instance, int defaultValue, params string[] names)
        {
            var value = GetMemberValue(type, instance, names);
            return value == null ? defaultValue : ConvertToInt(value);
        }

        private static bool HasMember(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null
                || type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
        }

        private static object GetMemberValue(Type type, object instance, params string[] names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                var field = type.GetField(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field.GetValue(instance);
                }

                var property = type.GetProperty(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    return property.GetValue(instance, null);
                }
            }

            return null;
        }

        private static int ConvertToInt(object value)
        {
            if (value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static string GetFailureMessage(Exception exception)
        {
            while (exception is TargetInvocationException invocationException && invocationException.InnerException != null)
            {
                exception = invocationException.InnerException;
            }

            return exception == null || string.IsNullOrEmpty(exception.Message)
                ? "Unknown Unity Console reader failure."
                : exception.GetType().Name + ": " + exception.Message;
        }
    }
}
