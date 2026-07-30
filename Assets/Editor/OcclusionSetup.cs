using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OcclusionSetup
{
    [MenuItem("Tools/Setup & Bake Occlusion Culling")]
    public static void SetupAndBake()
    {
        const string scenePath = "Assets/Scenes/Story mode.unity";
        EditorSceneManager.OpenScene(scenePath);

        var rootObjs = SceneManager.GetActiveScene().GetRootGameObjects();

        Bounds combined = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        int staticCount = 0;

        foreach (var root in rootObjs)
        {
            var mrs = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in mrs)
            {
                GameObject go = mr.gameObject;
                GameObjectUtility.SetStaticEditorFlags(go,
                    StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic);

                if (!hasBounds)
                {
                    combined = mr.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(mr.bounds);
                }
                staticCount++;
            }
        }

        Debug.Log($"Marked {staticCount} MeshRenderers as Occluder+Occludee Static.");

        if (hasBounds)
        {
            Vector3 size = combined.size;
            float maxDim = Mathf.Max(size.x, size.y, size.z);
            float padding = maxDim * 0.2f;

            var areaGO = new GameObject("OcclusionArea_Global");
            var area = areaGO.AddComponent<OcclusionArea>();
            area.center = combined.center;
            area.size = combined.size + Vector3.one * padding;

            Debug.Log($"Created OcclusionArea: center={area.center}, size={area.size}");
        }
        else
        {
            var areaGO = new GameObject("OcclusionArea_Global");
            var area = areaGO.AddComponent<OcclusionArea>();
            area.center = new Vector3(0f, 5f, 0f);
            area.size = new Vector3(500f, 50f, 500f);
            Debug.LogWarning("No MeshRenderers found; created default 500x50x500 OcclusionArea.");
        }

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("Starting occlusion bake...");
        StaticOcclusionCulling.Compute();
        Debug.Log("Occlusion bake completed.");

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Scene saved with occlusion data.");
    }

    [MenuItem("Tools/Bake Occlusion Only")]
    public static void BakeOnly()
    {
        Debug.Log("Starting occlusion bake...");
        StaticOcclusionCulling.Compute();
        Debug.Log("Occlusion bake completed.");
    }
}