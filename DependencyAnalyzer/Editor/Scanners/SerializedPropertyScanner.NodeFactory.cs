using System;
using System.Collections.Generic;
using DependencyAnalyzer.Editor.Core;
using DependencyAnalyzer.Editor.Settings;
using DependencyAnalyzer.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DependencyAnalyzer.Editor.Scanners
{
    public sealed partial class SerializedPropertyScanner
    {
        private static DependencyNodeData CreateObjectReferenceNode(
            UnityEngine.Object unityObject,
            DependencyCache cache,
            AnalyzerSettings settings)
        {
            var assetPath = AssetDatabase.GetAssetPath(unityObject);
            if (unityObject is MonoScript)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(assetPath))
            {
                if (AssetDatabase.IsValidFolder(assetPath) || settings.IsPathExcluded(assetPath))
                {
                    return null;
                }

                return AssetScanner.CreateAssetNode(assetPath, cache);
            }

            if (unityObject is GameObject || unityObject is Component)
            {
                var component = unityObject as Component;
                if (component != null && !ShouldVisualizeComponent(component))
                {
                    return CreateSceneObjectNode(component.gameObject, cache);
                }

                return CreateSceneObjectNode(unityObject, cache);
            }

            var type = unityObject.GetType();
            var globalObjectId = GetGlobalObjectId(unityObject);
            var node = new DependencyNodeData(
                BuildObjectId("object", unityObject, globalObjectId),
                globalObjectId,
                unityObject.name,
                unityObject.name,
                type.Name,
                type.FullName,
                0L,
                Array.Empty<string>(),
                IconUtility.GetIconContentName(type),
                DependencyNodeKind.SceneObject,
                unityObject.GetInstanceID());
            return cache.Store(node);
        }

        private static DependencyNodeData CreateSceneObjectNode(UnityEngine.Object unityObject, DependencyCache cache)
        {
            var type = unityObject.GetType();
            var globalObjectId = GetGlobalObjectId(unityObject);
            var path = GetObjectPath(unityObject);
            var node = new DependencyNodeData(
                BuildObjectId("scene", unityObject, globalObjectId),
                globalObjectId,
                path,
                GetDisplayName(unityObject),
                GetDisplayTypeName(unityObject, type),
                type.FullName,
                0L,
                Array.Empty<string>(),
                GetIconContentName(unityObject, type),
                unityObject is Component ? DependencyNodeKind.Component : DependencyNodeKind.SceneObject,
                unityObject.GetInstanceID());
            return cache.Store(node);
        }

        private static string GetDisplayName(UnityEngine.Object unityObject)
        {
            if (unityObject is MonoBehaviour monoBehaviour)
            {
                return monoBehaviour.GetType().Name + " (Script)";
            }

            if (unityObject is Component component)
            {
                return component.GetType().Name;
            }

            return unityObject.name;
        }

        private static string GetDisplayTypeName(UnityEngine.Object unityObject, Type type)
        {
            if (unityObject is MonoBehaviour)
            {
                return "Script";
            }

            if (unityObject is GameObject gameObject)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                {
                    return PrefabUtility.GetNearestPrefabInstanceRoot(gameObject) == gameObject
                        ? "Prefab Instance"
                        : "Prefab Child";
                }

                if (gameObject.GetComponent<Camera>() != null)
                {
                    return "Camera";
                }

                if (gameObject.GetComponent<Canvas>() != null)
                {
                    return "Canvas";
                }

                var light = gameObject.GetComponent<Light>();
                if (light != null)
                {
                    return light.type == LightType.Directional ? "Directional Light" : "Light";
                }

                return "Object";
            }

            return type.Name;
        }

        private static string GetMissingReferenceTypeName(SerializedProperty property)
        {
            if (property == null || string.IsNullOrEmpty(property.type))
            {
                return "Missing Reference";
            }

            const string pointerPrefix = "PPtr<$";
            var type = property.type;
            var start = type.IndexOf(pointerPrefix, StringComparison.Ordinal);
            if (start >= 0)
            {
                start += pointerPrefix.Length;
                var end = type.IndexOf('>', start);
                if (end > start)
                {
                    return NormalizeMissingReferenceTypeName(type.Substring(start, end - start));
                }
            }

            return NormalizeMissingReferenceTypeName(type);
        }

        private static string NormalizeMissingReferenceTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return "Missing Reference";
            }

            var lastDot = typeName.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < typeName.Length - 1)
            {
                typeName = typeName.Substring(lastDot + 1);
            }

            if (typeName == "MonoScript" || typeName == "MonoBehaviour" || typeName.EndsWith("Script", StringComparison.Ordinal))
            {
                return "Script";
            }

            if (typeName == "GameObject")
            {
                return "Object";
            }

            if (typeName == "Texture2D" || typeName == "Texture3D" || typeName == "Cubemap")
            {
                return "Texture";
            }

            if (typeName == "RuntimeAnimatorController" || typeName == "AnimatorController")
            {
                return "Animator";
            }

            return typeName;
        }

        private static string GetMissingReferenceNamespaceQualifiedTypeName(string typeName)
        {
            switch (typeName)
            {
                case "Script":
                    return "UnityEngine.MonoBehaviour";
                case "Object":
                    return "UnityEngine.GameObject";
                case "Missing Reference":
                    return "Missing Reference";
                default:
                    return "UnityEngine." + typeName;
            }
        }

        private static string GetMissingReferenceIconContentName(string typeName)
        {
            switch (typeName)
            {
                case "Script":
                    return "cs Script Icon";
                case "Object":
                    return "GameObject Icon";
                case "Material":
                    return "Material Icon";
                case "Texture":
                    return "Texture Icon";
                case "AudioClip":
                    return "AudioClip Icon";
                case "Mesh":
                    return "Mesh Icon";
                case "Camera":
                    return "Camera Icon";
                case "Canvas":
                    return "Canvas Icon";
                case "Light":
                    return "Light Icon";
                case "Animator":
                    return "Animator Icon";
                default:
                    return "DefaultAsset Icon";
            }
        }

        private static DependencyNodeKind GetMissingReferenceKind(string typeName)
        {
            switch (typeName)
            {
                case "Script":
                    return DependencyNodeKind.Component;
                case "Object":
                case "Camera":
                case "Canvas":
                case "Light":
                    return DependencyNodeKind.SceneObject;
                case "Missing Reference":
                    return DependencyNodeKind.MissingReference;
                default:
                    return DependencyNodeKind.Asset;
            }
        }

        private static string GetIconContentName(UnityEngine.Object unityObject, Type type)
        {
            if (unityObject is GameObject gameObject)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                {
                    return "Prefab Icon";
                }

                if (gameObject.GetComponent<Camera>() != null)
                {
                    return "Camera Icon";
                }

                if (gameObject.GetComponent<Canvas>() != null)
                {
                    return "Canvas Icon";
                }

                if (gameObject.GetComponent<Light>() != null)
                {
                    return "Light Icon";
                }
            }

            return IconUtility.GetIconContentName(type);
        }

        private static GlobalObjectId GetGlobalObjectId(UnityEngine.Object unityObject)
        {
            try
            {
                return GlobalObjectId.GetGlobalObjectIdSlow(unityObject);
            }
            catch (Exception)
            {
                return default;
            }
        }

        private static string BuildObjectId(string prefix, UnityEngine.Object unityObject, GlobalObjectId globalObjectId)
        {
            var globalObjectIdText = globalObjectId.ToString();
            if (!string.IsNullOrEmpty(globalObjectIdText))
            {
                return prefix + ":" + globalObjectIdText + ":" + unityObject.GetInstanceID();
            }

            return prefix + ":instance:" + unityObject.GetInstanceID();
        }

        private static string GetObjectPath(UnityEngine.Object unityObject)
        {
            if (unityObject is Component component)
            {
                return GetScenePath(component.gameObject.scene) + "::" + GetHierarchyPath(component.gameObject) + "/" + component.GetType().Name;
            }

            if (unityObject is GameObject gameObject)
            {
                return GetScenePath(gameObject.scene) + "::" + GetHierarchyPath(gameObject);
            }

            return unityObject.name;
        }

        private static string GetScenePath(Scene scene)
        {
            if (!string.IsNullOrEmpty(scene.path))
            {
                return scene.path;
            }

            return string.IsNullOrEmpty(scene.name) ? "Unsaved Scene" : scene.name;
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            var names = new Stack<string>();
            var current = gameObject.transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

    }
}
