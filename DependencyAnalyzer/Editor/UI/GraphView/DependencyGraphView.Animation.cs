using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.Editor.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DependencyAnalyzer.Editor.UI.GraphView
{
    public sealed partial class DependencyGraphView
    {

        private void AnimateLayoutTransition(
            IReadOnlyDictionary<string, Rect> previousRects,
            IReadOnlyDictionary<string, RenderSnapshot> previousSnapshots,
            IReadOnlyDictionary<string, Rect> finalRects)
        {
            if (ShouldSkipLayoutAnimation(previousRects, finalRects))
            {
                return;
            }

            var nodeAnimations = new List<NodeAnimation>();
            foreach (var pair in finalRects)
            {
                if (!nodeViews.TryGetValue(pair.Key, out var nodeView))
                {
                    continue;
                }

                var finalRect = pair.Value;
                var isNewNode = !previousRects.ContainsKey(pair.Key);
                var startRect = finalRect;
                if (!isNewNode && previousRects.TryGetValue(pair.Key, out var previousRect))
                {
                    startRect = previousRect;
                }
                else
                {
                    var parentRect = FindCurrentParentRect(pair.Key, finalRects, previousRects);
                    if (parentRect.HasValue)
                    {
                        startRect = new Rect(
                            parentRect.Value.center.x - finalRect.width * 0.5f,
                            parentRect.Value.center.y - finalRect.height * 0.5f,
                            finalRect.width,
                            finalRect.height);
                    }
                }

                if (isNewNode)
                {
                    nodeView.style.opacity = 0f;
                }

                if (!isNewNode && (startRect.position - finalRect.position).sqrMagnitude <= 0.5f)
                {
                    nodeView.SetGraphPosition(finalRect.position);
                    nodeView.style.opacity = nodeView.TargetOpacity;
                    nodeRects[pair.Key] = finalRect;
                    continue;
                }

                nodeView.SetGraphPosition(startRect.position);
                nodeRects[pair.Key] = new Rect(startRect.position, finalRect.size);
                nodeAnimations.Add(new NodeAnimation(pair.Key, nodeView, startRect.position, finalRect.position, isNewNode));
            }

            var ghostAnimations = CreateGhostAnimations(previousRects, previousSnapshots, finalRects);
            if (nodeAnimations.Count == 0 && ghostAnimations.Count == 0)
            {
                return;
            }

            var startTime = Time.realtimeSinceStartup;
            activeAnimation = schedule.Execute(() =>
            {
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - startTime) / AnimationDurationSeconds);
                var eased = SmoothStep(t);

                for (var i = 0; i < nodeAnimations.Count; i++)
                {
                    var animation = nodeAnimations[i];
                    var position = Vector2.Lerp(animation.StartPosition, animation.EndPosition, eased);
                    animation.View.SetGraphPosition(position);
                    if (animation.FadeIn)
                    {
                        animation.View.style.opacity = eased * animation.View.TargetOpacity;
                    }

                    if (finalRects.TryGetValue(animation.ViewId, out var finalRect))
                    {
                        nodeRects[animation.ViewId] = new Rect(position, finalRect.size);
                    }
                }

                for (var i = 0; i < ghostAnimations.Count; i++)
                {
                    var animation = ghostAnimations[i];
                    var position = Vector2.Lerp(animation.StartRect.position, animation.EndPosition, eased);
                    animation.View.style.left = position.x;
                    animation.View.style.top = position.y;
                    animation.View.style.opacity = animation.StartOpacity * (1f - eased);
                }

                RefreshEdges();
                miniMap.MarkDirtyRepaint();

                if (t < 1f)
                {
                    return;
                }

                for (var i = 0; i < nodeAnimations.Count; i++)
                {
                    var animation = nodeAnimations[i];
                    animation.View.SetGraphPosition(animation.EndPosition);
                    animation.View.style.opacity = animation.View.TargetOpacity;
                    if (finalRects.TryGetValue(animation.ViewId, out var finalRect))
                    {
                        nodeRects[animation.ViewId] = finalRect;
                    }
                }

                for (var i = 0; i < ghostAnimations.Count; i++)
                {
                    ghostAnimations[i].View.RemoveFromHierarchy();
                }

                activeAnimation?.Pause();
                activeAnimation = null;
                RefreshEdges();
                miniMap.MarkDirtyRepaint();
            }).Every(16);
        }

        private static bool ShouldSkipLayoutAnimation(
            IReadOnlyDictionary<string, Rect> previousRects,
            IReadOnlyDictionary<string, Rect> finalRects)
        {
            if (previousRects == null || finalRects == null)
            {
                return true;
            }

            var largestNodeCount = Mathf.Max(previousRects.Count, finalRects.Count);
            if (largestNodeCount > MaxAnimatedLayoutNodeCount)
            {
                return true;
            }

            var nodeDelta = Mathf.Abs(finalRects.Count - previousRects.Count);
            return nodeDelta > MaxAnimatedLayoutNodeDelta;
        }

        private List<GhostAnimation> CreateGhostAnimations(
            IReadOnlyDictionary<string, Rect> previousRects,
            IReadOnlyDictionary<string, RenderSnapshot> previousSnapshots,
            IReadOnlyDictionary<string, Rect> finalRects)
        {
            var ghosts = new List<GhostAnimation>();
            foreach (var pair in previousRects)
            {
                if (finalRects.ContainsKey(pair.Key) || !previousSnapshots.TryGetValue(pair.Key, out var snapshot))
                {
                    continue;
                }

                var targetRect = FindVisibleAncestorRect(snapshot.ParentViewId, previousSnapshots, finalRects);
                var endPosition = targetRect.HasValue
                    ? targetRect.Value.center - pair.Value.size * 0.5f
                    : pair.Value.position;
                var ghost = new VisualElement();
                ghost.AddToClassList("dependency-node-ghost");
                ghost.AddToClassList(DependencyNodeStyleResolver.GetNodeTypeClass(snapshot.Node));
                if (snapshot.Node.IsMissingTarget)
                {
                    ghost.AddToClassList(DependencyNodeStyleResolver.MissingTargetClass);
                }

                var startOpacity = DependencyNodeStyleResolver.GetNodeOpacity(snapshot.Node);
                ghost.style.position = Position.Absolute;
                ghost.style.left = pair.Value.x;
                ghost.style.top = pair.Value.y;
                ghost.style.width = pair.Value.width;
                ghost.style.height = pair.Value.height;
                ghost.style.opacity = startOpacity;
                animationLayer.Add(ghost);
                ghosts.Add(new GhostAnimation(ghost, pair.Value, endPosition, startOpacity));
            }

            return ghosts;
        }

        private Rect? FindCurrentParentRect(
            string viewId,
            IReadOnlyDictionary<string, Rect> finalRects,
            IReadOnlyDictionary<string, Rect> previousRects)
        {
            if (!renderNodeByViewId.TryGetValue(viewId, out var renderNode) || renderNode.Parent == null)
            {
                return null;
            }

            if (finalRects.TryGetValue(renderNode.Parent.ViewId, out var finalParentRect))
            {
                return finalParentRect;
            }

            if (previousRects.TryGetValue(renderNode.Parent.ViewId, out var previousParentRect))
            {
                return previousParentRect;
            }

            return null;
        }

        private static Rect? FindVisibleAncestorRect(
            string parentViewId,
            IReadOnlyDictionary<string, RenderSnapshot> previousSnapshots,
            IReadOnlyDictionary<string, Rect> finalRects)
        {
            var current = parentViewId;
            while (!string.IsNullOrEmpty(current))
            {
                if (finalRects.TryGetValue(current, out var rect))
                {
                    return rect;
                }

                if (!previousSnapshots.TryGetValue(current, out var snapshot))
                {
                    return null;
                }

                current = snapshot.ParentViewId;
            }

            return null;
        }

        private void StopActiveAnimation()
        {
            if (activeAnimation == null)
            {
                return;
            }

            activeAnimation.Pause();
            activeAnimation = null;
        }

        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
