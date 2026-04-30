using System;
using System.Threading;
using System.Threading.Tasks;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Scanners;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.UI.GraphView;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Controller
{
    public sealed class DependencyGraphController : IDisposable
    {
        private readonly DependencyGraphView graphView;
        private readonly ScannerOrchestrator scannerOrchestrator;
        private readonly DependencyCache cache;
        private readonly EditorSelectionSync selectionSync;
        private readonly Button scanButton;
        private readonly Button cancelButton;
        private readonly IntegerField depthField;
        private readonly Label statusLabel;

        private CancellationTokenSource scanCancellation;
        private DependencyGraphData currentGraph;

        public DependencyGraphController(VisualElement root, DependencyGraphView graphView)
        {
            this.graphView = graphView;
            scannerOrchestrator = new ScannerOrchestrator();
            cache = new DependencyCache();
            selectionSync = new EditorSelectionSync();

            scanButton = root.Q<Button>("scan-button");
            cancelButton = root.Q<Button>("cancel-button");
            depthField = root.Q<IntegerField>("depth-field");
            statusLabel = root.Q<Label>("status-label");

            if (scanButton != null)
            {
                scanButton.clicked += HandleScanClicked;
            }

            if (cancelButton != null)
            {
                cancelButton.clicked += CancelActiveScan;
                cancelButton.SetEnabled(false);
            }

            if (depthField != null)
            {
                depthField.value = Mathf.Clamp(depthField.value == 0 ? 3 : depthField.value, 3, 4);
                depthField.RegisterValueChangedCallback(HandleDepthChanged);
            }

            graphView.NodeSelected += HandleNodeSelected;
            SetStatus("Ready");
        }

        public void Dispose()
        {
            graphView.NodeSelected -= HandleNodeSelected;

            if (scanButton != null)
            {
                scanButton.clicked -= HandleScanClicked;
            }

            if (cancelButton != null)
            {
                cancelButton.clicked -= CancelActiveScan;
            }

            CancelActiveScan();
        }

        private void HandleScanClicked()
        {
            _ = ScanAsync();
        }

        private async Task ScanAsync()
        {
            if (scanCancellation != null)
            {
                return;
            }

            var activeCancellation = new CancellationTokenSource();
            scanCancellation = activeCancellation;

            var token = activeCancellation.Token;
            cache.Clear();
            SetScanControlsEnabled(false);
            SetStatus("Scanning");

            try
            {
                var settings = AnalyzerSettings.GetOrCreateSettings();
                var progress = new Progress<ScanProgress>(HandleScanProgress);
                currentGraph = await scannerOrchestrator.ScanAsync(settings, cache, progress, token);
                graphView.Populate(currentGraph, GetInitialDepth(settings));
                ReportIssues(currentGraph);
                SetStatus("Completed: " + currentGraph.Nodes.Count + " nodes, "
                    + currentGraph.Edges.Count + " edges, "
                    + currentGraph.Issues.Count + " issues");
            }
            catch (OperationCanceledException)
            {
                SetStatus("Canceled");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("Failed: " + exception.Message);
            }
            finally
            {
                if (scanCancellation == activeCancellation)
                {
                    activeCancellation.Dispose();
                    scanCancellation = null;
                }

                SetScanControlsEnabled(true);
            }
        }

        private int GetInitialDepth(AnalyzerSettings settings)
        {
            if (depthField == null)
            {
                return settings.InitialExpansionDepth;
            }

            var depth = Mathf.Clamp(depthField.value, 3, 4);
            if (depthField.value != depth)
            {
                depthField.value = depth;
            }

            return depth;
        }

        private void HandleScanProgress(ScanProgress progress)
        {
            var percentage = progress.Total <= 0 ? 0f : progress.Ratio * 100f;
            SetStatus(progress.ScannerName + ": " + progress.Message + " (" + percentage.ToString("0") + "%)");
        }

        private void HandleDepthChanged(ChangeEvent<int> evt)
        {
            var clampedValue = Mathf.Clamp(evt.newValue, 3, 4);
            if (depthField != null && depthField.value != clampedValue)
            {
                depthField.value = clampedValue;
                return;
            }

            if (currentGraph != null)
            {
                graphView.Populate(currentGraph, clampedValue);
            }
        }

        private void HandleNodeSelected(DependencyNodeData node)
        {
            graphView.ExpandNode(node.Id);
            selectionSync.PingAndSelect(node);
        }

        private void CancelActiveScan()
        {
            if (scanCancellation == null || scanCancellation.IsCancellationRequested)
            {
                return;
            }

            scanCancellation.Cancel();
        }

        private void SetScanControlsEnabled(bool enabled)
        {
            scanButton?.SetEnabled(enabled);
            cancelButton?.SetEnabled(!enabled);
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

        private static void ReportIssues(DependencyGraphData graphData)
        {
            if (graphData == null || graphData.Issues.Count == 0)
            {
                return;
            }

            foreach (var issue in graphData.Issues)
            {
                var message = "[" + issue.ScannerName + "] " + issue.SubjectPath + ": " + issue.Message;
                if (issue.Severity == DependencyScanIssueSeverity.Error)
                {
                    Debug.LogError(message);
                }
                else if (issue.Severity == DependencyScanIssueSeverity.Warning)
                {
                    Debug.LogWarning(message);
                }
                else
                {
                    Debug.Log(message);
                }
            }
        }
    }
}
