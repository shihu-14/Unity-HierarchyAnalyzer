using System;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.UI.Icons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class DependencyIconProviderTests
    {
        [Test]
        public void MissingScriptIcon_HasExpectedImporterSettings()
        {
            var importer = AssetImporter.GetAtPath(
                DependencyIconProvider.MissingScriptIconPath) as TextureImporter;

            Assert.NotNull(importer);
            Assert.AreEqual(TextureImporterType.Default, importer.textureType);
            Assert.IsTrue(importer.sRGBTexture);
            Assert.AreEqual(TextureImporterAlphaSource.None, importer.alphaSource);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.IsFalse(importer.isReadable);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
            Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
            Assert.AreEqual(256, importer.maxTextureSize);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.AreEqual(TextureImporterNPOTScale.None, importer.npotScale);
        }

        [Test]
        public void GetIcon_UsesCustomAssetOnlyForMissingScriptTarget()
        {
            var customIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                DependencyIconProvider.MissingScriptIconPath);
            var missingScript = CreateScriptNode("missing-script");
            missingScript.MarkAsMissingTarget(MissingTargetKind.MissingScript);
            var brokenScriptReference = CreateScriptNode("broken-script-reference");
            brokenScriptReference.MarkAsMissingTarget(MissingTargetKind.BrokenReference);
            var validScript = CreateScriptNode("valid-script");
            var builtInScriptIcon = EditorGUIUtility.IconContent("cs Script Icon").image;

            Assert.NotNull(customIcon);
            Assert.AreSame(customIcon, DependencyIconProvider.GetIcon(missingScript));
            Assert.AreSame(builtInScriptIcon, DependencyIconProvider.GetIcon(brokenScriptReference));
            Assert.AreSame(builtInScriptIcon, DependencyIconProvider.GetIcon(validScript));
        }

        private static DependencyNode CreateScriptNode(string id)
        {
            return new DependencyNode(
                id,
                default,
                "Scene::" + id,
                id,
                "Script",
                "UnityEngine.MonoBehaviour",
                Array.Empty<string>(),
                "cs Script Icon",
                DependencyNodeKind.Component);
        }
    }
}
