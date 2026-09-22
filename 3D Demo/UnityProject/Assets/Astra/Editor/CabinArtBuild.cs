using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using AstraCabin;

// Apply to the existing scene; the normal scene generator calls the same method.
public static class CabinArtBuild
{
    const string Root = "Assets/Astra";
    [MenuItem("Astra/Art/Apply industrial art sample")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(Root + "/Scenes/Cabin04.unity");
        ApplyToOpenScene();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    public static void ApplyToOpenScene()
    {
        // Preserve the authored rust, procedural wear and colour/detail textures.
        // The user explicitly prefers the existing weathered material character.
        Highlight("HullPaint", .22f);
        Highlight("DarkSteel", .26f);
        Highlight("Brass", .30f);
        Highlight("RedPaint", .23f);
        Signal("LampGreen", "6DC054", .65f);
        Signal("LampRed", "CE402B", .75f);
        Signal("LampWarm", "DBBC7E", .85f);
        var screen = Material("Screen");
        screen.SetColor("_Color", Hex("A8CE89")); screen.SetFloat("_Brightness", 1.25f);
        EditorUtility.SetDirty(screen);
        var grade = Material("Grade");
        grade.SetFloat("_Exposure", 1.06f); grade.SetFloat("_Vignette", .12f);
        grade.SetFloat("_StyleStrength", 1); grade.SetFloat("_ColorSteps", 24);
        grade.SetFloat("_DitherStrength", .65f); EditorUtility.SetDirty(grade);
        var camera = Camera.main;
        if (!camera) throw new Exception("No main camera in cabin scene");
        var presentation = camera.GetComponent<CabinPresentation>();
        presentation.grade = grade; presentation.sceneLook = CabinSceneLook.Pixel; presentation.pixelScale = 2;
        var budget = camera.GetComponent<CabinLightBudget>();
        if (!budget) budget = camera.gameObject.AddComponent<CabinLightBudget>();
        var systems = UnityEngine.Object.FindObjectOfType<CabinSystems>();
        budget.keyLights = systems.cabinLights.Where(l => l.type == LightType.Spot).ToArray();
        foreach (var light in systems.cabinLights)
        {
            light.shadows = LightShadows.None;
            if (light.type == LightType.Spot)
            {
                light.renderMode = LightRenderMode.ForcePixel;
                light.color = new Color(1, .92f, .76f); light.intensity = 2.15f;
                light.range = 3.0f; light.spotAngle = 112;
                int wall = Array.IndexOf(budget.keyLights, light);
                var rotation = Quaternion.Euler(0, wall * 90, 0);
                light.transform.position = rotation * new Vector3(0, 2.2f, .85f);
                light.transform.rotation = Quaternion.LookRotation(rotation * new Vector3(0, -.8f, .65f));
                light.shadowBias = .015f; light.shadowNormalBias = .012f;
                light.shadowResolution = LightShadowResolution.Medium;
            }
            else if (light.name.Contains("cage lamp"))
            {
                light.renderMode = LightRenderMode.Auto;
                light.color = new Color(1, .91f, .77f); light.intensity = .7f; light.range = 2.5f;
            }
            else { light.renderMode = LightRenderMode.ForcePixel; light.color = new Color(.79f, .83f, .75f); light.intensity = 1.1f; light.range = 4; }
        }
        // Initial editor view matches the front-facing runtime selection.
        foreach (var light in budget.keyLights.Take(2)) light.shadows = LightShadows.Soft;
        foreach (var light in systems.emergencyLights)
        {
            light.shadows = LightShadows.None;
            // Keep repair targets legible through the dim part of the alarm pulse.
            light.intensity = light.name.Contains("cabin bounce") ? 1.6f : .42f;
        }
        RenderSettings.ambientSkyColor = new Color(.16f, .18f, .155f);
        RenderSettings.ambientEquatorColor = new Color(.105f, .12f, .095f);
        RenderSettings.ambientGroundColor = new Color(.055f, .053f, .043f);
        QualitySettings.pixelLightCount = 4;
        QualitySettings.shadowDistance = 5;

        var cabin = GameObject.Find("Astra Cabin | Blender source");
        if (!cabin) throw new Exception("Missing cabin model root");
        foreach (var renderer in cabin.GetComponentsInChildren<Renderer>(true))
        {
            // The FBX batches lettering, dials and keys in the Paper material family.
            if (renderer.sharedMaterials.Any(m => m && m.name == "Paper"))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
        EditorUtility.SetDirty(camera.gameObject);
        EditorUtility.SetDirty(presentation); EditorUtility.SetDirty(budget);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("ASTRA_ART_APPLIED: original rust retained, 24 tones, stable dither, two spot shadows");
    }

    static Color Hex(string value) { Color c; ColorUtility.TryParseHtmlString("#" + value, out c); return c; }
    static Material Material(string name)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + name + ".mat");
        if (!material) throw new Exception("Missing art material: " + name);
        return material;
    }
    static void Highlight(string name, float smoothness)
    {
        var material = Material(name);
        material.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(material);
    }
    static void Signal(string name, string color, float energy)
    {
        var material = Material(name); var c = Hex(color);
        material.shader = Shader.Find("Astra/SignalLamp");
        material.color = c * .4f; material.SetColor("_EmissionColor", c.linear * energy);
        material.SetFloat("_Glossiness", .18f); EditorUtility.SetDirty(material);
    }
    [MenuItem("Astra/Art/Build Windows art sample")]
    public static void Build()
    {
        Apply();
        Directory.CreateDirectory("../BuildArt");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { Root + "/Scenes/Cabin04.unity" }, locationPathName = "../BuildArt/Astra Art Sample.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Art sample build failed: " + report.summary.result);
        Debug.Log("ASTRA_ART_BUILD_SUCCEEDED " + report.summary.totalSize);
    }
}
