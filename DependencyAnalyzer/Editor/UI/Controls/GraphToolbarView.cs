using System;
using DependencyAnalyzer.Editor.UI.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.Controls
{
    internal sealed class GraphToolbarView : IDisposable
    {
        private readonly Button loadButton;
        private readonly Label loadProgressLabel;
        private readonly SliderInt depthSlider;
        private readonly Label depthValueLabel;

        public GraphToolbarView(VisualElement root)
        {
            loadButton = root.Q<Button>("load-button");
            loadProgressLabel = root.Q<Label>("load-progress-label");
            depthSlider = root.Q<SliderInt>("depth-slider");
            depthValueLabel = root.Q<Label>("depth-value-label");
            if (loadButton != null)
            {
                loadButton.clicked += HandleLoadClicked;
            }

            if (depthSlider != null)
            {
                depthSlider.lowValue = DependencyGraphView.MinExpansionDepth;
                depthSlider.highValue = DependencyGraphView.AllExpansionDepthValue;
                depthSlider.pageSize = 1f;
                depthSlider.showInputField = false;
                depthSlider.RegisterValueChangedCallback(HandleDepthChanged);
            }

            SetLoading(false, false);
        }

        public event Action LoadRequested;
        public event Action<int> DepthChanged;

        public void SetLoading(bool loading, bool completed)
        {
            if (loadButton != null)
            {
                loadButton.SetEnabled(!loading);
                loadButton.text = completed ? "Reload" : "Load";
            }

            if (!loading && loadProgressLabel != null)
            {
                loadProgressLabel.text = string.Empty;
                loadProgressLabel.style.display = DisplayStyle.Flex;
            }
        }

        public void SetProgress(float ratio)
        {
            if (loadProgressLabel == null)
            {
                return;
            }

            var completed = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(ratio) * 100f), 0, 100);
            loadProgressLabel.text = completed + "/100";
            loadProgressLabel.style.display = DisplayStyle.Flex;
        }

        public void SetDepth(int depth)
        {
            depth = DependencyGraphView.ClampExpansionDepth(depth);
            depthSlider?.SetValueWithoutNotify(depth);
            if (depthValueLabel != null)
            {
                depthValueLabel.text = depth == DependencyGraphView.AllExpansionDepthValue ? "All" : depth.ToString();
            }
        }

        public void Dispose()
        {
            if (loadButton != null)
            {
                loadButton.clicked -= HandleLoadClicked;
            }

            depthSlider?.UnregisterValueChangedCallback(HandleDepthChanged);
            LoadRequested = null;
            DepthChanged = null;
        }

        private void HandleLoadClicked()
        {
            LoadRequested?.Invoke();
        }

        private void HandleDepthChanged(ChangeEvent<int> evt)
        {
            DepthChanged?.Invoke(evt.newValue);
        }
    }
}
