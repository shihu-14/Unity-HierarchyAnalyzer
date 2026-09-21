using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class SearchUiTests
    {
        private const string GraphWindowUxmlPath = "Assets/DependencyAnalyzer/Editor/UI/Styles/GraphWindow.uxml";
        private const string SearchStylePath = "Assets/DependencyAnalyzer/Editor/UI/Styles/SearchStyle.uss";

        [Test]
        public void SearchArrowButtons_ShareCircularHoverAndStrongerActiveStyles()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GraphWindowUxmlPath);
            Assert.IsNotNull(visualTree);
            var root = visualTree.CloneTree();
            var previous = root.Q<Button>("search-previous-button");
            var next = root.Q<Button>("search-next-button");

            Assert.IsTrue(previous.ClassListContains("dependency-search-arrow-button"));
            Assert.IsTrue(next.ClassListContains("dependency-search-arrow-button"));

            var styleText = File.ReadAllText(SearchStylePath).Replace("\r\n", "\n");
            var baseRule = ExtractStyleRule(
                styleText,
                ".dependency-search-control .dependency-search-arrow-button");
            var hoverRule = ExtractStyleRule(
                styleText,
                ".dependency-search-control .dependency-search-arrow-button:enabled:hover");
            var activeRule = ExtractStyleRule(
                styleText,
                ".dependency-search-control .dependency-search-arrow-button:enabled:active");

            StringAssert.Contains("width: 22px;", baseRule);
            StringAssert.Contains("height: 22px;", baseRule);
            StringAssert.Contains("border-top-left-radius: 10px;", baseRule);
            StringAssert.Contains("background-color: rgba(255, 255, 255, 0.12);", hoverRule);
            StringAssert.Contains("border-top-color: rgba(255, 255, 255, 0.35);", hoverRule);
            StringAssert.Contains("background-color: rgba(255, 255, 255, 0.28);", activeRule);
            StringAssert.Contains("border-top-color: rgba(255, 255, 255, 0.70);", activeRule);
        }

        [Test]
        public void GraphToolbar_ReplacesZoomScaleWithDiscreteDepthControl()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GraphWindowUxmlPath);
            Assert.IsNotNull(visualTree);
            var root = visualTree.CloneTree();

            Assert.IsNull(root.Q("zoom-step-control"));
            Assert.IsNull(root.Q("zoom-scale-label"));
            Assert.IsNull(root.Q("zoom-step-slider"));

            var depthSlider = root.Q<SliderInt>("depth-slider");
            Assert.IsNotNull(depthSlider);
            Assert.AreEqual(0, depthSlider.lowValue);
            Assert.AreEqual(6, depthSlider.highValue);
            Assert.AreEqual(2, depthSlider.value);
            Assert.IsFalse(depthSlider.showInputField);
            Assert.AreEqual("Depth", root.Q<Label>("depth-label").text);
            Assert.AreEqual("2", root.Q<Label>("depth-value-label").text);

            var styleText = File.ReadAllText(SearchStylePath).Replace("\r\n", "\n");
            var depthControlRule = ExtractStyleRule(styleText, ".dependency-depth-control");
            StringAssert.Contains("width: 196px;", depthControlRule);
        }

        private static string ExtractStyleRule(string styleText, string selector)
        {
            var signature = "\n" + selector + " {";
            var start = styleText.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "USS selector was not found: " + selector);
            start++;
            var end = styleText.IndexOf('}', start);
            Assert.Greater(end, start, "USS rule was not closed: " + selector);
            return styleText.Substring(start, end - start + 1);
        }
    }
}
