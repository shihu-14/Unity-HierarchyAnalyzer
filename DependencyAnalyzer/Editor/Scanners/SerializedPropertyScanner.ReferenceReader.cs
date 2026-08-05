using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Scanners
{
    internal enum SerializedObjectReferenceState
    {
        None,
        Valid,
        Missing,
        Unreadable
    }

    internal readonly struct SerializedObjectReferenceInfo
    {
        public SerializedObjectReferenceInfo(
            string propertyPath,
            string serializedTypeName,
            SerializedObjectReferenceState state,
            UnityEngine.Object referencedObject,
            int missingInstanceId,
            string errorMessage,
            string serializedDisplayValue = "")
        {
            PropertyPath = propertyPath ?? string.Empty;
            SerializedTypeName = serializedTypeName ?? string.Empty;
            State = state;
            ReferencedObject = referencedObject;
            MissingInstanceId = missingInstanceId;
            ErrorMessage = errorMessage ?? string.Empty;
            SerializedDisplayValue = serializedDisplayValue ?? string.Empty;
        }

        public string PropertyPath { get; }
        public string SerializedTypeName { get; }
        public SerializedObjectReferenceState State { get; }
        public UnityEngine.Object ReferencedObject { get; }
        public int MissingInstanceId { get; }
        public string ErrorMessage { get; }
        public string SerializedDisplayValue { get; }
    }

    internal interface ISerializedObjectReferenceReader
    {
        IEnumerable<SerializedObjectReferenceInfo> Read(Component component);
    }

    internal sealed class UnitySerializedObjectReferenceReader : ISerializedObjectReferenceReader
    {
        private static readonly Func<SerializedProperty, string> ObjectReferenceStringValueReader =
            CreateObjectReferenceStringValueReader();

        public IEnumerable<SerializedObjectReferenceInfo> Read(Component component)
        {
            if (component == null)
            {
                yield break;
            }

            using (var serializedObject = new SerializedObject(component))
            {
                serializedObject.UpdateIfRequiredOrScript();
                using (var property = serializedObject.GetIterator())
                {
                    while (MoveNextVisible(property, component))
                    {
                        if (!SerializedPropertyScanner.ShouldScanInspectorObjectReference(component, property))
                        {
                            continue;
                        }

                        var propertyPath = string.Empty;
                        var serializedTypeName = string.Empty;
                        var state = SerializedObjectReferenceState.Unreadable;
                        UnityEngine.Object referencedObject = null;
                        var missingInstanceId = 0;
                        var errorMessage = string.Empty;
                        var serializedDisplayValue = string.Empty;

                        try
                        {
                            propertyPath = property.propertyPath;
                            serializedTypeName = property.type;
                            referencedObject = property.objectReferenceValue;
                            if (referencedObject != null)
                            {
                                state = SerializedObjectReferenceState.Valid;
                            }
                            else
                            {
                                missingInstanceId = property.objectReferenceInstanceIDValue;
                                serializedDisplayValue = ReadObjectReferenceStringValue(property);
                                state = missingInstanceId != 0 || IsMissingDisplayValue(serializedDisplayValue)
                                    ? SerializedObjectReferenceState.Missing
                                    : SerializedObjectReferenceState.None;
                            }
                        }
                        catch (Exception exception)
                        {
                            errorMessage = exception.Message;
                        }

                        yield return new SerializedObjectReferenceInfo(
                            propertyPath,
                            serializedTypeName,
                            state,
                            referencedObject,
                            missingInstanceId,
                            errorMessage,
                            serializedDisplayValue);
                    }
                }
            }
        }

        private static bool MoveNextVisible(SerializedProperty property, Component component)
        {
            try
            {
                return property.NextVisible(true);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Failed to enumerate serialized properties for " + component.GetType().FullName,
                    exception);
            }
        }

        private static Func<SerializedProperty, string> CreateObjectReferenceStringValueReader()
        {
            var property = typeof(SerializedProperty).GetProperty(
                "objectReferenceStringValue",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var getter = property?.GetGetMethod(true);
            if (getter == null)
            {
                return null;
            }

            try
            {
                return (Func<SerializedProperty, string>)Delegate.CreateDelegate(
                    typeof(Func<SerializedProperty, string>),
                    getter);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (MethodAccessException)
            {
                return null;
            }
        }

        private static string ReadObjectReferenceStringValue(SerializedProperty property)
        {
            if (ObjectReferenceStringValueReader == null)
            {
                return string.Empty;
            }

            return ObjectReferenceStringValueReader(property) ?? string.Empty;
        }

        private static bool IsMissingDisplayValue(string displayValue)
        {
            return !string.IsNullOrEmpty(displayValue)
                && displayValue.IndexOf("Missing", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
