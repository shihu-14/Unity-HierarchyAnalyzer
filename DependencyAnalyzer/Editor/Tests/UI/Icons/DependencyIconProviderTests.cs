using System;
using System.Collections.Generic;
using System.IO;
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
            Assert.AreEqual(TextureImporterAlphaSource.FromInput, importer.alphaSource);
            Assert.IsTrue(importer.alphaIsTransparency);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.IsFalse(importer.isReadable);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
            Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
            Assert.AreEqual(256, importer.maxTextureSize);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.AreEqual(TextureImporterNPOTScale.None, importer.npotScale);
        }

        [Test]
        public void MissingScriptIcon_HasTransparentTightCanvasAndWhiteGlyph()
        {
            var bytes = File.ReadAllBytes(DependencyIconProvider.MissingScriptIconPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(texture, bytes, false));
                Assert.AreEqual(256, texture.width);
                Assert.AreEqual(256, texture.height);

                var pixels = texture.GetPixels32();
                Assert.AreEqual(0, pixels[0].a);
                Assert.AreEqual(0, pixels[texture.width - 1].a);
                Assert.AreEqual(0, pixels[(texture.height - 1) * texture.width].a);
                Assert.AreEqual(0, pixels[pixels.Length - 1].a);

                var minX = texture.width;
                var minY = texture.height;
                var maxX = -1;
                var maxY = -1;
                var nonTransparentPixelCount = 0;
                var partialAlphaValues = new HashSet<byte>();
                var hasOpaquePixel = false;
                for (var y = 0; y < texture.height; y++)
                {
                    for (var x = 0; x < texture.width; x++)
                    {
                        var pixel = pixels[y * texture.width + x];
                        if (pixel.a == 0)
                        {
                            continue;
                        }

                        nonTransparentPixelCount++;
                        hasOpaquePixel |= pixel.a == byte.MaxValue;
                        if (pixel.a < byte.MaxValue)
                        {
                            partialAlphaValues.Add(pixel.a);
                        }

                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                        Assert.AreEqual(byte.MaxValue, pixel.r);
                        Assert.AreEqual(byte.MaxValue, pixel.g);
                        Assert.AreEqual(byte.MaxValue, pixel.b);
                    }
                }

                Assert.Greater(nonTransparentPixelCount, 10000);
                Assert.IsTrue(hasOpaquePixel);
                Assert.Greater(partialAlphaValues.Count, 8);
                Assert.AreEqual(38, minX);
                Assert.AreEqual(16, minY);
                Assert.AreEqual(217, maxX);
                Assert.AreEqual(239, maxY);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
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
            Assert.AreEqual(256, customIcon.width);
            Assert.AreEqual(256, customIcon.height);
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
