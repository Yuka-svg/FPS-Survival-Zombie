using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;

public class CleanInstancedMaterials
{
    [MenuItem("Tools/Clean Instanced Materials")]
    public static void Clean()
    {
        var scene = SceneManager.GetActiveScene();
        var rootObjs = scene.GetRootGameObjects();
        int cleanedCount = 0;
        int totalRenderers = 0;

        // Cache materials to avoid redundant searches
        Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

        foreach (var root in rootObjs)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                totalRenderers++;
                var sharedMaterials = r.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    var mat = sharedMaterials[i];
                    if (mat == null) continue;

                    string assetPath = AssetDatabase.GetAssetPath(mat);
                    // If the asset path is empty or it is serialized directly in the scene (ends with .unity)
                    if (string.IsNullOrEmpty(assetPath) || assetPath.EndsWith(".unity"))
                    {
                        string cleanedName = mat.name.Replace(" (Instance)", "").Trim();
                        
                        // Try to find the original material asset
                        if (!materialCache.TryGetValue(cleanedName, out var originalMat))
                        {
                            string[] guids = AssetDatabase.FindAssets(cleanedName + " t:Material");
                            foreach (string guid in guids)
                            {
                                string path = AssetDatabase.GUIDToAssetPath(guid);
                                if (Path.GetFileNameWithoutExtension(path) == cleanedName)
                                {
                                    originalMat = AssetDatabase.LoadAssetAtPath<Material>(path);
                                    if (originalMat != null)
                                    {
                                        materialCache[cleanedName] = originalMat;
                                        break;
                                    }
                                }
                            }
                        }

                        if (originalMat != null)
                        {
                            sharedMaterials[i] = originalMat;
                            changed = true;
                            cleanedCount++;
                        }
                        else
                        {
                            Debug.LogWarning($"Could not find original material asset for instanced material: {mat.name} on GameObject {r.gameObject.name}", r.gameObject);
                        }
                    }
                }

                if (changed)
                {
                    r.sharedMaterials = sharedMaterials;
                    EditorUtility.SetDirty(r.gameObject);
                }
            }
        }

        if (cleanedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Successfully cleaned {cleanedCount} instanced materials across {totalRenderers} renderers in the active scene!");
        }
        else
        {
            Debug.Log("No instanced materials found in the active scene.");
        }
    }
}
