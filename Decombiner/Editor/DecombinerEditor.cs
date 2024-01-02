using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;

namespace Trev
{
    public class DecombinerEditor : EditorWindow
    {
        private enum RendererAction
        {
            Ignore,
            Disable,
            Destroy
        }

        private string _exportDirectory = "Assets/DecombinedMeshes/";
        private bool _resetPivot = false;
        private bool _resetScale = true;
        private bool _fixColliders = true;
        private bool _fixChildren = true;
        private bool _setMeshesStatic = true;
        private RendererAction _combinedRendAction = RendererAction.Disable;

        private bool _logDebug = false;

        private Regex _rootSceneRegex = new Regex("_\\(root.*?_?scene\\)");

        private string ProjectDirectory => Path.GetDirectoryName(Application.dataPath);

        [MenuItem("Window/Decombiner")]
        private static void Init()
        {
            DecombinerEditor window = (DecombinerEditor)GetWindow(typeof(DecombinerEditor), false, "Decombiner");
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Settings", EditorStyles.boldLabel);

            GUILayout.Space(2);

            _exportDirectory = EditorGUILayout.TextField(
                new GUIContent("Export Directory",
                "Where the exported sub-meshes are stored.\n" +
                "Meshes are formatted as <ExportDirectory>/<CombinedMeshName>/sub_<index>.asset."),
                _exportDirectory);

            GUILayout.Space(2);

            if (_resetPivot)
                EditorGUILayout.HelpBox("Pivoting meshes sometimes causes major position offsets.\n" +
                                        "We recommend backing up your scene before using this option.", MessageType.Warning);

            _resetPivot = EditorGUILayout.Toggle(
                new GUIContent("Center Pivot",
                "Centers the pivot of decombined meshes, "),
                _resetPivot);

            GUILayout.Space(2);

            if (!_resetScale)
                EditorGUILayout.HelpBox("Disabling this option can lead to position offsets in the final decombined mesh\n" +
                                        "We recommend backing up your scene before using this option.", MessageType.Warning);

            _resetScale = EditorGUILayout.Toggle(
                new GUIContent("Reset Scale",
                "Resets the scale of the decombined renderer which fixes position offset issues, though can mess up other things."),
                _resetScale);

            GUILayout.Space(2);

            if (_resetScale)
            {
                if (!_fixColliders)
                    EditorGUILayout.HelpBox("Disabling this option can lead to incorrectly sized colliders in the final decombined mesh if they exist.\n" +
                                            "We recommend backing up your scene before using this option.", MessageType.Warning);

                _fixColliders = EditorGUILayout.Toggle(
                    new GUIContent("Fix Collider Scale",
                    "Moves colliders on the root object to a child object with a corrected scale value to fix issues from Reset Scale."),
                    _fixColliders);

                GUILayout.Space(2);

                if (!_fixChildren)
                    EditorGUILayout.HelpBox("Disabling this option can lead to incorrectly sized children.\n" +
                                            "We recommend backing up your scene before using this option.", MessageType.Warning);

                _fixChildren = EditorGUILayout.Toggle(
                    new GUIContent("Fix Children Scale",
                    "Corrects an object's children when resetting the parent's scale."),
                    _fixChildren);

                GUILayout.Space(2);
            }

            _setMeshesStatic = EditorGUILayout.Toggle(
                    new GUIContent("Set Meshes Static",
                    "Sets the final decombined meshes to static."),
                    _setMeshesStatic);

            GUILayout.Space(2);

            _combinedRendAction = (RendererAction)EditorGUILayout.EnumPopup(
                new GUIContent("Combined Renderer Action",
                "What happens to the original combined mesh renderer when the decombined mesh is finalized."),
                _combinedRendAction);

            GUILayout.Space(10);
            GUILayout.Label("Debug Settings", EditorStyles.boldLabel);

            _logDebug = EditorGUILayout.Toggle(
                new GUIContent("Log Actions",
                "Logs information between decombining actions."),
                _logDebug);

            GUILayout.Space(25);

            if (GUILayout.Button("Decombine Active Scene"))
            {
                if (EditorUtility.DisplayDialog("Decombiner", "Are you sure you want to decombine the currently active scene?", "Yes", "Cancel"))
                    DecombScene();
            }

            GUILayout.Space(2);

            if (GUILayout.Button("Decombine Selected Objects"))
            {
                if (EditorUtility.DisplayDialog("Decombiner", "Are you sure you want to decombine all selected objects?", "Yes", "Cancel"))
                    DecombOnSelection();
            }
        }

        private void DecombOnSelection()
        {
            EditorUtility.DisplayProgressBar("Decombiner - Selection", "Decombining objects...", 0f);

            var mRenderers = Selection.gameObjects.SelectMany(g => g.GetComponentsInChildren<MeshRenderer>()).Where(m => m.isPartOfStaticBatch).ToArray();

            for (int i = 0; i < mRenderers.Length; i++)
            {
                try
                {
                    Process(mRenderers[i]);
                }
                catch (System.Exception e)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Decombiner - Selection", "An error occured while decombining your selection!\n" +
                        $"Error occured on renderer \"{mRenderers[i]}\"\n" +
                        $"Check the console for more information.", "Shoot");
                    throw e;
                }
                float progress = i / mRenderers.Length;
                EditorUtility.DisplayProgressBar("Decombiner - Selection", $"Decombining object {i + 1} of {mRenderers.Length}...", progress);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Decombiner - Selection", "Successfully decombined selection", "Nice");
        }

        private void DecombScene()
        {
            EditorUtility.DisplayProgressBar("Decombiner - Scene", "Decombining scene...", 0f);

            var mRenderers = GameObject.FindObjectsOfType<MeshRenderer>().Where(m => m.isPartOfStaticBatch).ToArray();

            for (int i = 0; i < mRenderers.Length; i++)
            {
                try
                {
                    Process(mRenderers[i]);
                }
                catch (System.Exception e)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Decombiner - Scene", "An error occured while decombining this scene!\n" +
                        $"Error occured on renderer \"{mRenderers[i]}\"\n" +
                        $"Check the console for more information.", "Shoot");
                    throw e;
                }
                float progress = i / mRenderers.Length;
                EditorUtility.DisplayProgressBar("Decombiner - Scene", $"Decombining scene object {i + 1} of {mRenderers.Length}...", progress);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Decombiner - Scene", "Successfully decombined scene", "Nice");
        }

        private void Process(MeshRenderer mr, bool refreshAssetDatabase = false, bool throwNonCombinedWarning = true)
        {
            // Make sure this wasn't previously decombined
            if (mr.GetComponent<DecombinedMeshScaleHolder>() || !mr.enabled || !mr.gameObject.activeInHierarchy)
                return;

            // Safety when decombining multiple scenes
            // Since different scenes can share combined mesh indexes, this will prevent overlap
            string _exportDirectory = this._exportDirectory;

            string sceneGuid = AssetDatabase.AssetPathToGUID(SceneManager.GetActiveScene().path);
            _exportDirectory += $"{sceneGuid}/";

            if (!AssetDatabase.IsValidFolder(_exportDirectory))
                // AssetDatabase.CreateFolder didn't work
                Directory.CreateDirectory(Path.Combine(ProjectDirectory, _exportDirectory));

            MeshFilter filter = mr.GetComponent<MeshFilter>();
            if (filter.sharedMesh == null)
                return;

            SerializedObject serializedRenderer = new SerializedObject(mr);

            if (!mr.isPartOfStaticBatch)
            {
                if (throwNonCombinedWarning)
                    Debug.LogWarning("Attempted decombination of a non-combined mesh!");
                return;
            }

            int startIndex = serializedRenderer.FindProperty("m_StaticBatchInfo.firstSubMesh").intValue;
            int endIndex = startIndex + serializedRenderer.FindProperty("m_StaticBatchInfo.subMeshCount").intValue;

            if (_logDebug)
                Debug.Log($"Start Index: {startIndex}, End Index: {endIndex}, Submeshes: {mr.sharedMaterials.Length}");

            if (_resetScale)
            {
                Transform[] children = null;
                Vector3 originalScale = mr.transform.localScale;
                mr.gameObject.AddComponent<DecombinedMeshScaleHolder>().originalScale = originalScale;

                if (_fixChildren)
                {
                    children = new Transform[mr.transform.childCount];
                    for (int i = 0; i < mr.transform.childCount; i++)
                        children[i] = mr.transform.GetChild(i);
                    mr.transform.DetachChildren();
                }

                mr.transform.localScale = Vector3.one;

                if (_fixChildren)
                    foreach (var child in children)
                        child.parent = mr.transform;

                if (_fixColliders)
                {
                    Collider[] colliders = mr.GetComponents<Collider>();
                    if (colliders.Length > 0)
                    {
                        GameObject newColliderHolder = new GameObject("ColliderHolder");
                        newColliderHolder.transform.SetParent(mr.transform, false);
                        newColliderHolder.transform.localScale = originalScale;

                        // Transfer GameObject settings like tags and layers
                        newColliderHolder.tag = mr.gameObject.tag;
                        newColliderHolder.layer = mr.gameObject.layer;

                        foreach (var c in colliders)
                        {
                            UnityEditorInternal.ComponentUtility.CopyComponent(c);
                            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(newColliderHolder);
                            DestroyImmediate(c);
                        }
                    }
                }
            }

            for (int i = startIndex; i < endIndex; i++)
            {
                int indexFromZero = i - startIndex;

                if (_logDebug)
                    Debug.Log("Writing sub-mesh " + indexFromZero);
                Mesh mesh = null;

                // Generate paths
                string meshFileName = filter.sharedMesh.name.Replace(' ', '_');
               
                foreach (char c in Path.GetInvalidFileNameChars())
                    meshFileName.Replace(c, '_');

                meshFileName = _rootSceneRegex.Replace(meshFileName, "");
                string folderPath = Combine(_exportDirectory, meshFileName);

                string subMeshFileName = $"sub_{i}.asset";
                string subMeshFilePath = Combine(folderPath, subMeshFileName);

                if (_logDebug)
                    Debug.Log("Sub-mesh file path: " + subMeshFilePath);

                // Reuse existing mesh if it has already been separated
                if (File.Exists(Path.Combine(ProjectDirectory, subMeshFilePath)))
                {
                    mesh = AssetDatabase.LoadAssetAtPath<Mesh>(subMeshFilePath);
                    if (mesh == null)
                    {
                        Debug.LogError($"Sub-mesh {i} for \"{mr.name}\" exists but is null in AssetDatabase!");
                        continue;
                    }
                }
                // Create save directory and save sub-mesh
                else
                {
                    if (!AssetDatabase.IsValidFolder(folderPath))
                        // AssetDatabase.CreateFolder didn't work
                        Directory.CreateDirectory(Path.Combine(ProjectDirectory, folderPath));

                    mesh = GetSubmesh(filter.sharedMesh, i);
                    AssetDatabase.CreateAsset(mesh, subMeshFilePath);
                }

                if (mesh == null)
                {
                    Debug.LogError($"Submesh {i} for \"{mr.name}\" couldn't be decombined!");
                    continue;
                }

                // Generate children with decombined sub-mesh
                GameObject childMesh = new GameObject($"SubMesh_{indexFromZero}");
                childMesh.transform.parent = mr.transform;

                // Transfer GameObject settings like tags and layers
                childMesh.layer = mr.gameObject.layer;
                childMesh.tag = mr.gameObject.tag;

                // Copy these components so all settings remain the same
                UnityEditorInternal.ComponentUtility.CopyComponent(mr);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(childMesh);

                var newRend = childMesh.GetComponent<MeshRenderer>();

                UnityEditorInternal.ComponentUtility.CopyComponent(filter);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(childMesh);

                var newFilter = childMesh.GetComponent<MeshFilter>();
                
                // Enable static batching
                if (_setMeshesStatic)
                    childMesh.isStatic = true;
                if (_resetPivot) // TODO: this is pretty broken, should probably figure out why
                    UpdatePivot(childMesh.transform, mesh);

                newRend.sharedMaterials = new Material[] {
                    mr.sharedMaterials[indexFromZero],
                };
                newFilter.sharedMesh = mesh;
            }

            if (refreshAssetDatabase)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            switch (_combinedRendAction)
            {
                case RendererAction.Ignore:
                    break;
                case RendererAction.Disable:
                    mr.enabled = false;
                    break;
                case RendererAction.Destroy:
                    DestroyImmediate(mr);
                    DestroyImmediate(filter);
                    break;
            }
        }

        private string Combine(string path1, string path2)
        {
            char ch = path1[path1.Length - 1];
            if (ch != '/' && ch != '\\' && ch != ':')
                return path1 + "/" + path2;
            return path1 + path2;
        }

        private static void UpdatePivot(Transform transform, Mesh mesh)
        {
            // Calculate last pivot
            Bounds b = mesh.bounds;
            Vector3 offset = -1 * b.center;
            Vector3 lastPivot = new Vector3(offset.x / b.extents.x, offset.y / b.extents.y, offset.z / b.extents.z);

            Vector3 diff = Vector3.Scale(mesh.bounds.extents, lastPivot - Vector3.zero); //Calculate difference in 3d position
            transform.position -= Vector3.Scale(diff, transform.localScale); //Move object position
                                                                             //Iterate over all vertices and move them in the opposite direction of the object position movement
            Vector3[] verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] += diff;
            }
            mesh.vertices = verts; //Assign the vertex array back to the mesh
            mesh.RecalculateBounds(); //Recalculate bounds of the mesh, for the renderer's sake
                                      //The 'center' parameter of certain colliders needs to be adjusted
                                      //when the transform position is modified
        }

        // https://answers.unity.com/questions/1213025/separating-submeshes-into-unique-meshes.html
        private class Vertices
        {
            List<Vector3> verts = null;
            List<Vector2> uv1 = null;
            List<Vector2> uv2 = null;
            List<Vector2> uv3 = null;
            List<Vector2> uv4 = null;
            List<Vector3> normals = null;
            List<Vector4> tangents = null;
            List<Color32> colors = null;
            List<BoneWeight> boneWeights = null;

            public Vertices()
            {
                verts = new List<Vector3>();
            }
            public Vertices(Mesh aMesh)
            {
                verts = CreateList(aMesh.vertices);
                uv1 = CreateList(aMesh.uv);
                uv2 = CreateList(aMesh.uv2);
                uv3 = CreateList(aMesh.uv3);
                uv4 = CreateList(aMesh.uv4);
                normals = CreateList(aMesh.normals);
                tangents = CreateList(aMesh.tangents);
                colors = CreateList(aMesh.colors32);
                boneWeights = CreateList(aMesh.boneWeights);
            }

            private List<T> CreateList<T>(T[] aSource)
            {
                if (aSource == null || aSource.Length == 0)
                    return null;
                return new List<T>(aSource);
            }
            private void Copy<T>(ref List<T> aDest, List<T> aSource, int aIndex)
            {
                if (aSource == null)
                    return;
                if (aDest == null)
                    aDest = new List<T>();
                aDest.Add(aSource[aIndex]);
            }
            public int Add(Vertices aOther, int aIndex)
            {
                int i = verts.Count;
                Copy(ref verts, aOther.verts, aIndex);
                Copy(ref uv1, aOther.uv1, aIndex);
                Copy(ref uv2, aOther.uv2, aIndex);
                Copy(ref uv3, aOther.uv3, aIndex);
                Copy(ref uv4, aOther.uv4, aIndex);
                Copy(ref normals, aOther.normals, aIndex);
                Copy(ref tangents, aOther.tangents, aIndex);
                Copy(ref colors, aOther.colors, aIndex);
                Copy(ref boneWeights, aOther.boneWeights, aIndex);
                return i;
            }
            public void AssignTo(Mesh aTarget)
            {
                aTarget.SetVertices(verts);
                if (uv1 != null) aTarget.SetUVs(0, uv1);
                if (uv2 != null) aTarget.SetUVs(1, uv2);
                if (uv3 != null) aTarget.SetUVs(2, uv3);
                if (uv4 != null) aTarget.SetUVs(3, uv4);
                if (normals != null) aTarget.SetNormals(normals);
                if (tangents != null) aTarget.SetTangents(tangents);
                if (colors != null) aTarget.SetColors(colors);
                if (boneWeights != null) aTarget.boneWeights = boneWeights.ToArray();
            }
        }

        private Mesh GetSubmesh(Mesh aMesh, int aSubMeshIndex)
        {
            if (aSubMeshIndex < 0 || aSubMeshIndex >= aMesh.subMeshCount)
                return null;
            int[] indices = aMesh.GetTriangles(aSubMeshIndex);
            Vertices source = new Vertices(aMesh);
            Vertices dest = new Vertices();
            Dictionary<int, int> map = new Dictionary<int, int>();
            int[] newIndices = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                int o = indices[i];
                int n;
                if (!map.TryGetValue(o, out n))
                {
                    n = dest.Add(source, o);
                    map.Add(o, n);
                }
                newIndices[i] = n;
            }
            Mesh m = new Mesh();
            dest.AssignTo(m);
            m.triangles = newIndices;
            return m;
        }
    }
}