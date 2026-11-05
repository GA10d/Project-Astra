using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AstraCabin;
using AstraCabin.Galaxies.Zyl;

public static class ZylStomachIntegration
{
    [MenuItem("Astra/Zyl/Configure stomach environment integration")]
    public static void Configure()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var existing = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ZylStomachEnvironment.ScenePath);
        if (existing.IsValid() && existing.isLoaded && existing.isDirty)
            throw new InvalidOperationException("请先保存胃内场景；集成工具不会覆盖未保存修改。");
        try
        {
            var scene = existing.IsValid() && existing.isLoaded ? existing : EditorSceneManager.OpenScene(ZylStomachEnvironment.ScenePath, OpenSceneMode.Additive);
            var roots = scene.GetRootGameObjects();
            if (roots.Any(r => r.GetComponentInChildren<CabinController>(true))) throw new InvalidOperationException("StomachRefuge 必须仅包含环境，不能包含玩家船。");
            var anchor = roots.SelectMany(r => r.GetComponentsInChildren<StomachEnvironmentSettings>(true)).FirstOrDefault();
            if (!anchor)
            {
                var go = new GameObject("Stomach cabin landing anchor | runtime alignment");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.SetPositionAndRotation(new Vector3(3.9f, .26f, 6), Quaternion.Euler(0, 340, 0));
                anchor = go.AddComponent<StomachEnvironmentSettings>();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save environment scene");
            var scenes = EditorBuildSettings.scenes.Where(s => !s.path.EndsWith("StomachRefuge_Playable.unity")).ToList();
            if (!scenes.Any(s => s.path == ZylStomachEnvironment.ScenePath)) scenes.Add(new EditorBuildSettingsScene(ZylStomachEnvironment.ScenePath, true));
            else scenes.First(s => s.path == ZylStomachEnvironment.ScenePath).enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
            const string obsolete = "Assets/Astra/Scenes/Zyl/StomachRefuge_Playable.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(obsolete) && !AssetDatabase.DeleteAsset(obsolete)) throw new IOException("Cannot delete obsolete playable scene");
            AssetDatabase.SaveAssets();
            Debug.Log("STOMACH_INTEGRATION_CONFIGURED environment-only, anchor added, additive build entry, obsolete scene removed");
        }
        finally
        {
            if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.OpenScene("Assets/Astra/Scenes/Cabin04.unity", OpenSceneMode.Single);
        }
    }
    public static void ConfigureAndBuild() { Configure(); CabinBuild.BuildDevelopment(); }
}
