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
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Create dependency issue root");
            }

            CreateMeshFilterWithoutMesh(root.transform);
            CreateRendererWithEmptyMaterial(root.transform);
            CreateAudioSourceWithoutClip(root.transform);
            CreateAnimatorWithoutController(root.transform);
            CreateSkinnedMeshWithoutMesh(root.transform);
            EditorUtility.SetDirty(root);
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
