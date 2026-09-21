using System;
using DependencyAnalyzer.Editor.Controller;
using DependencyAnalyzer.Editor.Core;
using NUnit.Framework;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class EditorSelectionSyncTests
    {
        private GameObject gameObject;
        private UnityEngine.Object selectedObject;
        private UnityEngine.Object pingedObject;
        private EditorSelectionSync selectionSync;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("Selection Sync Test");
            selectedObject = null;
            pingedObject = null;
            selectionSync = new EditorSelectionSync(
                target => selectedObject = target,
                target => pingedObject = target);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void GameObject_SelectsAndPingsGameObject()
        {
            Assert.IsTrue(selectionSync.PingAndSelect(gameObject));

            Assert.AreSame(gameObject, selectedObject);
            Assert.AreSame(gameObject, pingedObject);
        }

        [Test]
        public void Component_SelectsComponentAndPingsOwningGameObject()
        {
            var component = gameObject.AddComponent<BoxCollider>();

            Assert.IsTrue(selectionSync.PingAndSelect(component));

            Assert.AreSame(component, selectedObject);
            Assert.AreSame(gameObject, pingedObject);
        }

        [Test]
        public void Asset_SelectsAndPingsSameObject()
        {
            var asset = ScriptableObject.CreateInstance<SelectionSyncAsset>();
            try
            {
                Assert.IsTrue(selectionSync.PingAndSelect(asset));

                Assert.AreSame(asset, selectedObject);
                Assert.AreSame(asset, pingedObject);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void MissingNode_DoesNotResolveOrSelectObject()
        {
            var missing = new DependencyNode(
                "missing",
                default,
                "Scene::Missing",
                "Missing",
                "Material",
                "UnityEngine.Material",
                Array.Empty<string>(),
                "Material Icon",
                DependencyNodeKind.Asset);
            missing.MarkAsMissingTarget(MissingTargetKind.BrokenReference);

            Assert.IsNull(selectionSync.ResolveObject(missing));
            Assert.IsFalse(selectionSync.PingAndSelect(missing));
            Assert.IsNull(selectedObject);
            Assert.IsNull(pingedObject);
        }

        private sealed class SelectionSyncAsset : ScriptableObject
        {
        }
    }
}
