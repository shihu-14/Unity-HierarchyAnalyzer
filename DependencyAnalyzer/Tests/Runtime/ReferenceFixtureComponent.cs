using System;
using System.Collections.Generic;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Tests
{
    [Serializable]
    public sealed class NestedReferenceFixture
    {
        public UnityEngine.Object reference;
    }

    public sealed class ReferenceFixtureComponent : MonoBehaviour
    {
        public GameObject gameObjectReference;
        public Component componentReference;
        public UnityEngine.Object assetReference;
        public UnityEngine.Object secondAssetReference;
        public UnityEngine.Object noneReference;
        public NestedReferenceFixture nested = new NestedReferenceFixture();
        public List<UnityEngine.Object> references = new List<UnityEngine.Object>();
    }
}
