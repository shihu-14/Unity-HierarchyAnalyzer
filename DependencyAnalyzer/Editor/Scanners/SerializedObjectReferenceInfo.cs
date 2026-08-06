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
}
