using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.DebugTools
{
    public static class DependencyIssueDebugBuilder
    {
        private const string RootName = "Dependency Analyzer Debug Issues";
        private const string IssueAssetFolder = "Assets/DependencyAnalyzerDemo/IssueAssets";
        private const string ConsoleWarningObjectName = "Issue_Console_Warning_Context";
        private const string ConsoleErrorObjectName = "Issue_Console_Error_Context";
        private const string ConsoleWarningScriptPath = IssueAssetFolder + "/IssueConsoleWarningBehaviour.cs";
        private const string ConsoleErrorShaderPath = IssueAssetFolder + "/IssueConsoleErrorShader.shader";
        private const string ConsoleErrorMaterialPath = IssueAssetFolder + "/IssueConsoleErrorMaterial.mat";

        [MenuItem("Tools/Dependency Analyzer/Create Debug Issue Objects")]
        public static void CreateDebugIssueObjects()
        {
            CreateConsoleIssueAssets();
            var root = GetOrCreateRoot();

            CreateMeshFilterWithoutMesh(root.transform);
            CreateRendererWithEmptyMaterial(root.transform);
            CreateAudioSourceWithoutClip(root.transform);
            CreateAnimatorWithoutController(root.transform);
            CreateSkinnedMeshWithoutMesh(root.transform);
            CreateMissingRigidbodyReference(root.transform);
            CreateObjectWithChildAndReferenceDetails(root.transform);
            CreateConsoleIssueObjects(root.transform);
            EditorUtility.SetDirty(root);
        }

        [MenuItem("Tools/Dependency Analyzer/Create Debug Toggle Detail Object")]
        public static void CreateDebugToggleDetailObject()
        {
            var root = GetOrCreateRoot();
            CreateObjectWithChildAndReferenceDetails(root.transform);
            EditorUtility.SetDirty(root);
        }

        [MenuItem("Tools/Dependency Analyzer/Emit Debug Console Issues")]
        public static void EmitDebugConsoleIssues()
        {
            CreateConsoleIssueAssets();
            var root = GetOrCreateRoot();
            CreateConsoleIssueObjects(root.transform);
            EditorUtility.SetDirty(root);
        }

        private static GameObject GetOrCreateRoot()
        {
            var root = GameObject.Find(RootName);
            if (root != null)
            {
                return root;
            }

            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create dependency issue root");
            return root;
        }

        private static void CreateMeshFilterWithoutMesh(Transform parent)
        {
            var gameObject = CreateCaseObject(parent, "Issue_MeshFilter_NoMesh", new Vector3(0f, 0f, 0f));
            var meshFilter = EnsureComponent<MeshFilter>(gameObject);
            meshFilter.sharedMesh = null;
            var renderer = EnsureComponent<MeshRenderer>(gameObject);
            renderer.sharedMaterials = new Material[] { null };
        }

        private static void CreateRendererWithEmptyMaterial(Transform parent)
        {
            var gameObject = CreatePrimitiveCaseObject(parent, "Issue_Renderer_EmptyMaterial", PrimitiveType.Cube);
            gameObject.transform.localPosition = new Vector3(2.4f, 0f, 0f);
            var renderer = EnsureComponent<MeshRenderer>(gameObject);
            renderer.sharedMaterials = new Material[] { null };
        }

        private static void CreateAudioSourceWithoutClip(Transform parent)
        {
            var gameObject = CreateCaseObject(parent, "Issue_AudioSource_NoClip", new Vector3(4.8f, 0f, 0f));
            var audioSource = EnsureComponent<AudioSource>(gameObject);
            audioSource.playOnAwake = true;
            audioSource.clip = null;
        }

        private static void CreateAnimatorWithoutController(Transform parent)
        {
            var gameObject = CreateCaseObject(parent, "Issue_Animator_NoController", new Vector3(7.2f, 0f, 0f));
            var animator = EnsureComponent<Animator>(gameObject);
            animator.runtimeAnimatorController = null;
        }

        private static void CreateSkinnedMeshWithoutMesh(Transform parent)
        {
            var gameObject = CreateCaseObject(parent, "Issue_SkinnedMesh_NoMesh", new Vector3(9.6f, 0f, 0f));
            var renderer = EnsureComponent<SkinnedMeshRenderer>(gameObject);
            renderer.sharedMesh = null;
        }

        private static void CreateMissingRigidbodyReference(Transform parent)
        {
            var gameObject = CreateCaseObject(parent, "Issue_FixedJoint_MissingConnectedBody", new Vector3(12f, 0f, 0f));
            var joint = EnsureComponent<FixedJoint>(gameObject);
            var target = new GameObject("Issue_FixedJoint_DeletedBody_Source");
            Undo.RegisterCreatedObjectUndo(target, "Create dependency issue target");
            target.transform.SetParent(parent);
            target.transform.localPosition = new Vector3(12f, -1.4f, 0f);
            var rigidbody = Undo.AddComponent<Rigidbody>(target);
            joint.connectedBody = rigidbody;
            EditorUtility.SetDirty(joint);
            Undo.DestroyObjectImmediate(target);
        }

        private static void CreateObjectWithChildAndReferenceDetails(Transform parent)
        {
            var gameObject = CreatePrimitiveCaseObject(parent, "Debug_BothToggleAndDetails_Parent", PrimitiveType.Cube);
            gameObject.transform.localPosition = new Vector3(14.4f, 0f, 0f);

            var renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    var material = new Material(shader) { name = "Debug_BothToggleAndDetails_Material" };
                    Undo.RegisterCreatedObjectUndo(material, "Create dependency issue material");
                    renderer.sharedMaterial = material;
                }
            }

            var child = CreatePrimitiveCaseObject(gameObject.transform, "Debug_BothToggleAndDetails_Child", PrimitiveType.Sphere);
            child.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            child.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
        }

        private static void CreateConsoleIssueObjects(Transform parent)
        {
            var warningObject = CreatePrimitiveCaseObject(parent, ConsoleWarningObjectName, PrimitiveType.Sphere);
            warningObject.transform.localPosition = new Vector3(16.8f, 0f, 0f);
            TryAddComponent(warningObject, "DependencyAnalyzerDemo.IssueConsoleWarningBehaviour, Assembly-CSharp");

            var errorObject = CreatePrimitiveCaseObject(parent, ConsoleErrorObjectName, PrimitiveType.Capsule);
            errorObject.transform.localPosition = new Vector3(19.2f, 0f, 0f);
            var renderer = EnsureComponent<MeshRenderer>(errorObject);
            var material = GetOrCreateConsoleErrorMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static GameObject CreateCaseObject(Transform parent, string name, Vector3 position)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                Undo.RecordObject(existing, "Update dependency issue object");
                existing.localPosition = position;
                return existing.gameObject;
            }

            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create dependency issue object");
            gameObject.transform.SetParent(parent);
            gameObject.transform.localPosition = position;
            return gameObject;
        }

        private static GameObject CreatePrimitiveCaseObject(Transform parent, string name, PrimitiveType primitiveType)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var gameObject = GameObject.CreatePrimitive(primitiveType);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create dependency issue object");
            gameObject.name = name;
            gameObject.transform.SetParent(parent);
            return gameObject;
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(gameObject);
        }

        private static void TryAddComponent(GameObject gameObject, string assemblyQualifiedTypeName)
        {
            var type = Type.GetType(assemblyQualifiedTypeName);
            if (type == null || gameObject.GetComponent(type) != null)
            {
                return;
            }

            Undo.AddComponent(gameObject, type);
        }

        private static Material GetOrCreateConsoleErrorMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ConsoleErrorShaderPath);
            if (shader == null)
            {
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(ConsoleErrorMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, ConsoleErrorMaterialPath);
                return material;
            }

            material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateConsoleIssueAssets()
        {
            EnsureAssetFolder(IssueAssetFolder);
            WriteTextIfDifferent(ConsoleWarningScriptPath, GetConsoleWarningScriptContents());
            WriteTextIfDifferent(ConsoleErrorShaderPath, GetConsoleErrorShaderContents());
            AssetDatabase.ImportAsset(ConsoleWarningScriptPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ConsoleErrorShaderPath, ImportAssetOptions.ForceUpdate);
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath) || string.Equals(folderPath, "Assets", StringComparison.Ordinal))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }

        private static void WriteTextIfDifferent(string assetPath, string contents)
        {
            var absolutePath = ToAbsolutePath(assetPath);
            if (File.Exists(absolutePath) && string.Equals(File.ReadAllText(absolutePath), contents, StringComparison.Ordinal))
            {
                return;
            }

            File.WriteAllText(absolutePath, contents);
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        private static string GetConsoleWarningScriptContents()
        {
            return "#warning Analyzer demo warning: IssueConsoleWarningBehaviour is intentionally warning for node-linked issue display.\n"
                + "using UnityEngine;\n\n"
                + "namespace DependencyAnalyzerDemo\n"
                + "{\n"
                + "    public sealed class IssueConsoleWarningBehaviour : MonoBehaviour\n"
                + "    {\n"
                + "        public string note = \"This script intentionally emits a compiler warning for the analyzer issue panel.\";\n"
                + "    }\n"
                + "}\n";
        }

        private static string GetConsoleErrorShaderContents()
        {
            return "Shader \"DependencyAnalyzerDemo/IssueConsoleErrorShader\"\n"
                + "{\n"
                + "    SubShader\n"
                + "    {\n"
                + "        Pass\n"
                + "        {\n"
                + "            HLSLPROGRAM\n"
                + "            #pragma vertex vert\n"
                + "            #pragma fragment frag\n\n"
                + "            float4 vert(float4 vertex : POSITION) : SV_POSITION\n"
                + "            {\n"
                + "                return vertex;\n"
                + "            }\n\n"
                + "            float4 frag() : SV_Target\n"
                + "            {\n"
                + "                return missingShaderSymbol;\n"
                + "            }\n"
                + "            ENDHLSL\n"
                + "        }\n"
                + "    }\n"
                + "}\n";
        }
    }
}
