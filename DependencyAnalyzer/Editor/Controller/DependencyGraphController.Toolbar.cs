using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Scanners;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.UI.Controls;
using DependencyAnalyzer.Editor.UI.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Controller
{
    public sealed partial class DependencyGraphController
    {
        private void ApplyGraphSettings(AnalyzerSettings settings)
        {
            graphView.ConfigureZoom(
                AnalyzerSettings.DefaultZoomMin,
                AnalyzerSettings.DefaultZoomMax,
                settings.ZoomStep);
        }

        private void HandleScanProgress(ScanProgress progress, CancellationTokenSource activeCancellation)
        {
            if (!isLoading || !ReferenceEquals(scanCancellation, activeCancellation))
            {
                return;
            }

            SetLoadProgress(progress.Total <= 0 ? 0f : progress.Ratio);
        }

        private void InitializeDepthFields(AnalyzerSettings settings)
        {
            currentExpansionDepth = DependencyGraphView.ClampExpansionDepth(settings.InitialExpansionDepth);
            if (depthSlider != null)
            {
                depthSlider.lowValue = DependencyGraphView.MinExpansionDepth;
                depthSlider.highValue = DependencyGraphView.AllExpansionDepthValue;
                depthSlider.pageSize = 1f;
                depthSlider.showInputField = false;
                depthSlider.SetValueWithoutNotify(currentExpansionDepth);
                depthSlider.RegisterValueChangedCallback(HandleDepthChanged);
            }

            UpdateDepthValueLabel();
        }

        private void HandleDepthChanged(ChangeEvent<int> evt)
        {
            SetExpansionDepth(evt.newValue);
        }

        internal void SetExpansionDepth(int depth)
        {
            var nextDepth = DependencyGraphView.ClampExpansionDepth(depth);
            if (depthSlider != null && depthSlider.value != nextDepth)
            {
                depthSlider.SetValueWithoutNotify(nextDepth);
            }

            if (currentExpansionDepth == nextDepth)
            {
                UpdateDepthValueLabel();
                return;
            }

            currentExpansionDepth = nextDepth;
            UpdateDepthValueLabel();
            graphView.SetExpansionDepth(currentExpansionDepth);
        }

        private void UpdateDepthValueLabel()
        {
            if (depthValueLabel == null)
            {
                return;
            }

            depthValueLabel.text = currentExpansionDepth == DependencyGraphView.AllExpansionDepthValue
                ? "All"
                : currentExpansionDepth.ToString();
        }

        internal void CancelActiveScan()
        {
            pendingRescan = false;
            if (scanCancellation == null || scanCancellation.IsCancellationRequested)
            {
                return;
            }

            scanCancellation.Cancel();
        }

        private void SetLoadControlsEnabled(bool enabled)
        {
            loadButton?.SetEnabled(enabled);
        }

        private void UpdateLoadButtonText()
        {
            if (loadButton != null)
            {
                loadButton.text = hasCompletedLoad ? "Reload" : "Load";
            }
        }

        private void SetLoadProgress(float ratio)
        {
            if (loadProgressLabel == null)
            {
                return;
            }

            var completed = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(ratio) * 100f), 0, 100);
            loadProgressLabel.text = completed + "/100";
            loadProgressLabel.style.display = DisplayStyle.Flex;
        }

        private void HideLoadProgress()
        {
            if (loadProgressLabel != null)
            {
                loadProgressLabel.text = string.Empty;
                loadProgressLabel.style.display = DisplayStyle.Flex;
            }
        }

        private static void SetSearchArrowIcon(Button button, bool pointsUp)
        {
            SetChevronButtonIcon(button, pointsUp, 0.45f);
        }

        private static void SetChevronButtonIcon(Button button, bool pointsUp, float verticalScale)
        {
            button.text = string.Empty;
            button.Clear();
            var icon = new ChevronIcon(pointsUp, verticalScale);
            icon.StretchToParentSize();
            button.Add(icon);
        }

    }
}
