using System;
using System.Reflection;
using DependencyAnalyzer.Editor.UI.Controls;
using DependencyAnalyzer.Editor.UI.GraphView;
using DependencyAnalyzer.Editor.UI.Issues;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class GraphControlsTests
    {
        [Test]
        public void Toolbar_ReflectsLoadingCompletionProgressAndDepth()
        {
            var root = new VisualElement();
            var load = new Button { name = "load-button" };
            var progress = new Label { name = "load-progress-label" };
            var depth = new SliderInt { name = "depth-slider" };
            var depthLabel = new Label { name = "depth-value-label" };
            root.Add(load);
            root.Add(progress);
            root.Add(depth);
            root.Add(depthLabel);
            using (var view = new GraphToolbarView(root))
            {
                Assert.AreEqual("Load", load.text);
                view.SetLoading(true, false);
                view.SetProgress(0.425f);
                Assert.IsFalse(load.enabledSelf);
                Assert.AreEqual("42/100", progress.text);
                view.SetLoading(false, true);
                Assert.IsTrue(load.enabledSelf);
                Assert.AreEqual("Reload", load.text);
                Assert.AreEqual(string.Empty, progress.text);
                view.SetDepth(0);
                Assert.AreEqual(0, depth.value);
                Assert.AreEqual("0", depthLabel.text);
                view.SetDepth(6);
                Assert.AreEqual("All", depthLabel.text);
            }
        }

        [Test]
        public void Toolbar_DisposeDisconnectsLoadRequests()
        {
            var root = new VisualElement();
            var button = new Button { name = "load-button" };
            root.Add(button);
            var view = new GraphToolbarView(root);
            var requests = 0;
            view.LoadRequested += () => requests++;
            Click(button);
            Assert.AreEqual(1, requests);
            view.Dispose();
            Click(button);
            Assert.AreEqual(1, requests);
        }

        [Test]
        public void Search_UpdatesCounterCandidatesAndNavigationAvailability()
        {
            var root = CreateSearchRoot();
            root.Q<TextField>("search-field").SetValueWithoutNotify("Audio");
            using (var view = new GraphSearchView(root))
            {
                var suggestion = new DependencyGraphView.SearchSuggestion("audio", "Audio Source", "Component", "Main/Audio");
                view.SetSearchState(new DependencyGraphView.SearchResultState(0, 1, new[] { suggestion }));
                Assert.AreEqual("1 / 1", root.Q<Label>("search-count-label").text);
                Assert.IsTrue(root.Q<Button>("search-next-button").enabledSelf);
                Assert.AreEqual("Audio Source", root.Q<Label>(className: "dependency-search-suggestion-title").text);
                view.SetSearchState(new DependencyGraphView.SearchResultState(-1, 0));
                Assert.AreEqual("0 / 0", root.Q<Label>("search-count-label").text);
                Assert.IsFalse(root.Q<Button>("search-next-button").enabledSelf);
                Assert.AreEqual(DisplayStyle.None, root.Q("search-suggestion-list").style.display.value);
            }
        }

        [Test]
        public void Search_DisposeDisconnectsNavigationRequests()
        {
            var root = CreateSearchRoot();
            var view = new GraphSearchView(root);
            var requests = 0;
            var reverse = false;
            view.NavigationRequested += previous => { requests++; reverse = previous; };
            view.SetSearchState(new DependencyGraphView.SearchResultState(0, 2));
            Click(root.Q<Button>("search-previous-button"));
            Assert.AreEqual(1, requests);
            Assert.IsTrue(reverse);
            Click(root.Q<Button>("search-next-button"));
            Assert.AreEqual(2, requests);
            Assert.IsFalse(reverse);
            view.Dispose();
            Click(root.Q<Button>("search-next-button"));
            Assert.AreEqual(2, requests);
        }

        [Test]
        public void Issues_PreserveExpansionAcrossRefreshAndRouteLocationClicks()
        {
            var root = CreateIssueRoot();
            var location = new ProjectIssueLocation(new[] { "Main", "Player" }, "m_Material", "Renderer", "renderer");
            var model = new ProjectIssuePanelModel(new[]
            {
                new ProjectIssueGroup("material", "Material", null, Color.cyan, new[] { location })
            });
            using (var view = new IssuePanelView(root))
            {
                var focusedId = string.Empty;
                view.NodeFocusRequested += nodeId => focusedId = nodeId;
                view.SetModel(model);
                Assert.AreEqual("1", root.Q<Label>("issue-warning-count-label").text);
                Assert.IsNull(root.Q(className: "dependency-issue-location-row"));
                Click(root.Q<Button>(className: "dependency-issue-group-row"));
                view.SetModel(model);
                Click(root.Q<Button>(className: "dependency-issue-location-row"));
                Assert.AreEqual("renderer", focusedId);
                view.SetModel(new ProjectIssuePanelModel(null));
                Assert.AreEqual("0", root.Q<Label>("issue-warning-count-label").text);
                Assert.AreEqual("No issues", root.Q<Label>(className: "dependency-issue-empty").text);
            }
        }

        [Test]
        public void Issues_ToggleRestoresHeightAndDisposeDisconnectsToggle()
        {
            var root = CreateIssueRoot();
            var view = new IssuePanelView(root);
            var panel = root.Q("issue-panel");
            var initialHeight = panel.style.height.value.value;
            var toggle = root.Q<Button>("issue-toggle-button");
            Click(toggle);
            Assert.AreEqual(DisplayStyle.None, root.Q("issue-list").style.display.value);
            Assert.Less(panel.style.height.value.value, initialHeight);
            Click(toggle);
            Assert.AreEqual(initialHeight, panel.style.height.value.value);
            view.Dispose();
            Click(toggle);
            Assert.AreEqual(DisplayStyle.Flex, root.Q("issue-list").style.display.value);
        }

        private static VisualElement CreateSearchRoot()
        {
            var root = new VisualElement();
            var control = new VisualElement { name = "search-control" };
            var wrap = new VisualElement { name = "search-field-wrap" };
            wrap.Add(new TextField { name = "search-field" });
            wrap.Add(new VisualElement { name = "search-suggestion-list" });
            control.Add(wrap);
            control.Add(new Button { name = "search-previous-button" });
            control.Add(new Button { name = "search-next-button" });
            control.Add(new Label { name = "search-count-label" });
            root.Add(control);
            return root;
        }

        private static VisualElement CreateIssueRoot()
        {
            var root = new VisualElement();
            var panel = new VisualElement { name = "issue-panel" };
            panel.Add(new VisualElement { name = "issue-resize-handle" });
            panel.Add(new ScrollView { name = "issue-list" });
            panel.Add(new Label { name = "issue-title-label" });
            panel.Add(new Label { name = "issue-warning-count-label" });
            panel.Add(new Button { name = "issue-toggle-button" });
            root.Add(panel);
            return root;
        }

        private static void Click(Button button)
        {
            var method = typeof(Clickable).GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var parameters = method.GetParameters();
            var arguments = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                arguments[i] = parameters[i].ParameterType.IsValueType
                    ? Activator.CreateInstance(parameters[i].ParameterType)
                    : null;
            }

            method.Invoke(button.clickable, arguments);
        }
    }
}
