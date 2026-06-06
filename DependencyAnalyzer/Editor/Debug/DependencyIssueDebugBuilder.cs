using UnityEditor;
using UnityEngine;

namespace DependencyAnalyzer.Editor.DebugTools
{
    public static class DependencyIssueDebugBuilder
    {
        private const string RootName = "Dependency Analyzer Debug Issues";

        [MenuItem("Tools/Dependency Analyzer/Create Debug Issue Objects")]
        public static void CreateDebugIssueObjects()
        {
            var root = GetOrCreateRoot();

            CreateMeshFilterWithoutMesh(root.transform);
            CreateRendererWithEmptyMaterial(root.transform);
            CreateAudioSourceWithoutClip(root.transform);
            CreateAnimatorWithoutController(root.transform);
            CreateSkinnedMeshWithoutMesh(root.transform);
            CreateObjectWithChildAndReferenceDetails(root.transform);
            EditorUtility.SetDirty(root);
        }

        [MenuItem("Tools/Dependency Analyzer/Create Debug Toggle Detail Object")]
        public static void CreateDebugToggleDetailObject()
        {
            var root = GetOrCreateRoot();
            CreateObjectWithChildAndReferenceDetails(root.transform);
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
            gameObject.AddComponent<MeshFilter>();
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new Material[] { null };
        }

        private static void CreateRendererWithEmptyMaterial(Transform parent)
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create dependency issue object");
            gameObject.name = "Issue_Renderer_EmptyMaterial";
            gameObject.transform.SetParent(parent);
            gameObject.transform.localPosition = new Vector3(2.4f, 0f, 0f);
            var renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new Material[] { null };
        }

        private static void CreateAudioSourceWithoutClip(Transform parent)
        {
            var gameObject = CreateCaseObject(parent, "Issue_AudioSource_NoClip", new Vector3(4.8f, 0f, 0f));
            var audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = true;
            audioSource.clip = null;
        }

        private static void CreateAnimatorWithoutController(Transform parent)
        {
            var gameObject = CreateCaseObject(parent, "Issue_Animator_NoController", new Vector3(7.2f, 0f, 0f));
            gameObject.AddComponent<Animator>();
        }

        private static void CreateSkinnedMeshWithoutMesh(Transform parent)
        {
            var gameObject = CreateCaseObject(parent, "Issue_SkinnedMesh_NoMesh", new Vector3(9.6f, 0f, 0f));
            gameObject.AddComponent<SkinnedMeshRenderer>();
        }

        private static void CreateObjectWithChildAndReferenceDetails(Transform parent)
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create dependency issue object");
            gameObject.name = "Debug_BothToggleAndDetails_Parent";
            gameObject.transform.SetParent(parent);
            gameObject.transform.localPosition = new Vector3(12f, 0f, 0f);

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

            var child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(child, "Create dependency issue child object");
            child.name = "Debug_BothToggleAndDetails_Child";
            child.transform.SetParent(gameObject.transform);
            child.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            child.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
        }

        private static GameObject CreateCaseObject(Transform parent, string name, Vector3 position)
        {
            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create dependency issue object");
            gameObject.transform.SetParent(parent);
            gameObject.transform.localPosition = position;
            return gameObject;
        }
    }
}
