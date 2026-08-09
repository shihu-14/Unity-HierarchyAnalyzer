using System;
using System.Threading;
using System.Threading.Tasks;
using DependencyAnalyzer.Editor.Controller;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Scanners;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.UI.GraphView;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.Tests
{
    public sealed class DependencyGraphControllerScanTests
    {
        [Test]
        public async Task RequestsDuringScan_AreCoalescedAndRescanLatestState()
        {
            var firstScanRelease = new TaskCompletionSource<bool>();
            var scanCount = 0;
            var activeScanCount = 0;
            var maximumActiveScanCount = 0;
            var hierarchyVersion = 1;
            var controller = CreateController(async cancellationToken =>
            {
                var scannedVersion = hierarchyVersion;
                scanCount++;
                activeScanCount++;
                maximumActiveScanCount = Math.Max(maximumActiveScanCount, activeScanCount);
                try
                {
                    if (scanCount == 1)
                    {
                        using (cancellationToken.Register(() => firstScanRelease.TrySetCanceled()))
                        {
                            await firstScanRelease.Task;
                        }
                    }

                    return CreateVersionedGraph(scannedVersion);
                }
                finally
                {
                    activeScanCount--;
                }
            });

            try
            {
                var activeRequest = controller.RequestScanAsync();
                Assert.AreEqual(1, scanCount);

                hierarchyVersion = 2;
                await controller.RequestScanAsync();
                await controller.RequestScanAsync();
                await controller.RequestScanAsync();

                Assert.IsTrue(controller.HasPendingRescan);
                firstScanRelease.SetResult(true);
                await activeRequest;

                Assert.AreEqual(2, scanCount);
                Assert.AreEqual(1, maximumActiveScanCount);
                Assert.IsFalse(controller.HasPendingRescan);
                Assert.IsTrue(controller.CurrentGraph.TryGetNode("version:2", out _));
                Assert.IsFalse(controller.CurrentGraph.TryGetNode("version:1", out _));
            }
            finally
            {
                controller.Dispose();
            }
        }

        [Test]
        public async Task CancelActiveScan_DropsPendingRetry()
        {
            var scanStarted = new TaskCompletionSource<bool>();
            var scanCount = 0;
            var controller = CreateController(async cancellationToken =>
            {
                scanCount++;
                scanStarted.SetResult(true);
                await WaitForCancellation(cancellationToken);
                return new DependencyGraph();
            });

            try
            {
                var activeRequest = controller.RequestScanAsync();
                await scanStarted.Task;
                await controller.RequestScanAsync();
                Assert.IsTrue(controller.HasPendingRescan);

                controller.CancelActiveScan();
                await activeRequest;

                Assert.AreEqual(1, scanCount);
                Assert.IsFalse(controller.HasPendingRescan);
            }
            finally
            {
                controller.Dispose();
            }
        }

        [Test]
        public async Task Dispose_DoesNotStartPendingOrFutureScan()
        {
            var scanStarted = new TaskCompletionSource<bool>();
            var scanCount = 0;
            var controller = CreateController(async cancellationToken =>
            {
                scanCount++;
                scanStarted.SetResult(true);
                await WaitForCancellation(cancellationToken);
                return new DependencyGraph();
            });

            var activeRequest = controller.RequestScanAsync();
            await scanStarted.Task;
            await controller.RequestScanAsync();
            Assert.IsTrue(controller.HasPendingRescan);

            controller.Dispose();
            await activeRequest;
            await controller.RequestScanAsync();

            Assert.AreEqual(1, scanCount);
            Assert.IsFalse(controller.HasPendingRescan);
        }

        [Test]
        public async Task DepthSelection_UpdatesImmediatelyAndPersistsAcrossReloads()
        {
            var root = new VisualElement();
            var depthSlider = new SliderInt { name = "depth-slider", lowValue = 1, highValue = 6, value = 2 };
            var depthValueLabel = new Label { name = "depth-value-label" };
            root.Add(depthSlider);
            root.Add(depthValueLabel);
            var graphView = new DependencyGraphView();
            var scanCount = 0;
            var controller = new DependencyGraphController(
                root,
                graphView,
                (AnalyzerSettings settings,
                    DependencyNodeCache cache,
                    IProgress<ScanProgress> progress,
                    CancellationToken cancellationToken) =>
                {
                    scanCount++;
                    return Task.FromResult(CreateVersionedGraph(scanCount));
                },
                false);

            try
            {
                controller.SetExpansionDepth(4);
                Assert.AreEqual(4, controller.CurrentExpansionDepth);
                Assert.AreEqual(4, graphView.ExpansionDepth);
                Assert.AreEqual("4", depthValueLabel.text);

                await controller.RequestScanAsync();
                await controller.RequestScanAsync();

                Assert.AreEqual(2, scanCount);
                Assert.AreEqual(4, controller.CurrentExpansionDepth);
                Assert.AreEqual(4, graphView.ExpansionDepth);
                Assert.AreEqual(AnalyzerSettings.DefaultZoomStep, graphView.ZoomStep);

                controller.SetExpansionDepth(DependencyGraphView.AllExpansionDepthValue);
                Assert.AreEqual("All", depthValueLabel.text);
                Assert.AreEqual(DependencyGraphView.AllExpansionDepthValue, graphView.ExpansionDepth);
            }
            finally
            {
                controller.Dispose();
            }
        }

        private static DependencyGraphController CreateController(
            Func<CancellationToken, Task<DependencyGraph>> scanOperation)
        {
            return new DependencyGraphController(
                new VisualElement(),
                new DependencyGraphView(),
                (AnalyzerSettings settings,
                    DependencyNodeCache cache,
                    IProgress<ScanProgress> progress,
                    CancellationToken cancellationToken) => scanOperation(cancellationToken),
                false);
        }

        private static DependencyGraph CreateVersionedGraph(int version)
        {
            var graph = new DependencyGraph();
            graph.AddOrUpdateNode(new DependencyNode(
                "version:" + version,
                default,
                "Scene::Version " + version,
                "Version " + version,
                "GameObject",
                "UnityEngine.GameObject",
                Array.Empty<string>(),
                "GameObject Icon",
                DependencyNodeKind.SceneObject,
                version));
            return graph;
        }

        private static Task WaitForCancellation(CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<bool>();
            cancellationToken.Register(() => completion.TrySetCanceled());
            return completion.Task;
        }
    }
}
