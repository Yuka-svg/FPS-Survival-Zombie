using UnityEditor;
using UnityEngine;

public class OcclusionBaker
{
    [MenuItem("Tools/Bake Occlusion")]
    public static void BakeOcclusion()
    {
        Debug.Log("Starting occlusion culling bake...");
        StaticOcclusionCulling.Compute();
        Debug.Log("Occlusion culling bake completed.");
    }
}
