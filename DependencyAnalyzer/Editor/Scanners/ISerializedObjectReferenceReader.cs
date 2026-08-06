using System.Collections.Generic;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Scanners
{
    internal interface ISerializedObjectReferenceReader
    {
        IEnumerable<SerializedObjectReferenceInfo> Read(Component component);
    }
}
