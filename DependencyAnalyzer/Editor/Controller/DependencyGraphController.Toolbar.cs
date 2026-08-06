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
            var step = zoomStepSlider == null ? settings.ZoomStep : zoomStepSlider.value;

            step = Mathf.Clamp(step, 0.001f, 0.03f);
            SetZoomSliderValue(step);
            graphView.ConfigureZoom(AnalyzerSettings.DefaultZoomMin, AnalyzerSettings.DefaultZoomMax, step);
        }

        private void HandleScanProgress(ScanProgress progress, CancellationTokenSource activeCancellation)
        {
            if (!isLoading || !ReferenceEquals(scanCancellation, activeCancellation))
            {
                return;
            }

            SetLoadProgress(progress.Total <= 0 ? 0f : progress.Ratio);
        }

        private void InitializeZoomFields(AnalyzerSettings settings)
        {
            SetZoomSliderValue(settings.ZoomStep);
            if (zoomStepSlider != null)
            {
                zoomStepSlider.RegisterValueChangedCallback(HandleZoomChanged);
            }
        }

        private void HandleZoomChanged(ChangeEvent<float> evt)
        {
            ApplyGraphSettings(AnalyzerSettings.LoadOrCreateRuntimeSettings());
        }

        private void SetZoomSliderValue(float value)
        {
            if (zoomStepSlider == null)
            {
                return;
            }

            zoomStepSlider.SetValueWithoutNotify(value);
        }

        private void CancelActiveScan()
        {
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

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

    }
}
