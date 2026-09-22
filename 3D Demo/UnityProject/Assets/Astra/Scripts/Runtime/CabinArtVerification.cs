using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AstraCabin
{
    // Only installed for --astra-art-qa <directory>. Ordinary play has no test loop.
    public sealed class CabinArtVerification : MonoBehaviour
    {
        string output;
        readonly List<string> report = new List<string>();
        int failures, errors;
        bool finished;
        float started;
        CabinController controller;
        CabinPresentation presentation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--astra-art-qa") < 0) return;
            var controller = FindObjectOfType<CabinController>();
            if (controller) controller.gameObject.AddComponent<CabinArtVerification>();
        }
        void Check(string name, bool passed, string detail = "")
        {
            report.Add((passed ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? " | " + detail : ""));
            if (!passed) failures++;
        }
        void Log(string text, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            { errors++; report.Add("ERROR " + text + "\n" + stack); }
        }
        void Update()
        {
            if (output != null && !finished && Time.realtimeSinceStartup - started > 90)
            { Check("Art QA watchdog", false); Finish(); }
        }
        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--astra-art-qa");
            if (index < 0 || index + 1 >= args.Length) yield break;
            output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output);
            started = Time.realtimeSinceStartup; Application.logMessageReceived += Log;
            controller = GetComponent<CabinController>(); controller.automationMode = true;
            presentation = controller.viewCamera.GetComponent<CabinPresentation>();
            var systems = GetComponent<CabinSystems>(); var director = GetComponent<SetpieceDirector>();
            director.automationMode = true;
            Application.runInBackground = true; Application.targetFrameRate = 60;
            yield return new WaitForSecondsRealtime(1);
            Check("Art grade is supported", presentation.grade && presentation.grade.shader.isSupported);
            Check("Desktop uses isolated QA storage", GetComponent<CabinComputer>().IsTestStorage);
            controller.SnapToWall(0); controller.SetPaused(true);
            presentation.sceneLook = CabinSceneLook.Clear;
            Color32[] clear = Capture("01_A_Clear");
            presentation.sceneLook = CabinSceneLook.Dithered;
            Color32[] dither = Capture("02_A_Dither");
            Check("Tone quantization changes the rendered image", Difference(clear, dither) > .015f);
            yield return new WaitForSecondsRealtime(.1f);
            Color32[] repeated = Capture(null);
            Check("Frozen scene has no temporal dither flicker", Difference(dither, repeated) < .001f);
            presentation.sceneLook = CabinSceneLook.Pixel;
            Capture("03_A_Pixel"); Capture("04_A_720p", 1280, 720);
            Check("Shared grade stays unchanged when selecting looks", presentation.grade.GetFloat("_StyleStrength") == 1);
            presentation.CycleLook(); Check("Pixel look cycles to clear", presentation.sceneLook == CabinSceneLook.Clear);
            presentation.CycleLook(); presentation.CycleLook();
            Check("All three looks cycle back to pixel", presentation.sceneLook == CabinSceneLook.Pixel);
            for (int wall = 0; wall < 4; wall++)
            {
                controller.SnapToWall(wall); Capture("05_Wall_" + wall);
                int shadows = systems.cabinLights.Count(l => l.enabled && l.shadows != LightShadows.None);
                Check("At most two cabin shadow lights at wall " + wall, shadows <= 2, "lights=" + shadows);
                Check("No six-face point-light shadows at wall " + wall,
                    systems.cabinLights.All(l => l.type != LightType.Point || l.shadows == LightShadows.None));
            }
            controller.SnapToWall(0); systems.SetMainPower(false); Capture("06_A_PowerOff");
            Check("Pixel look preserves power controls", !systems.MainPowerOn && systems.cabinLights.All(l => !l.enabled));
            systems.SetMainPower(true); controller.SetPaused(false);
            controller.SnapToWall(1); controller.EnterWindowView();
            yield return new WaitForSecondsRealtime(1);
            Capture("07_D_Window"); controller.ExitWindowView();
            yield return new WaitForSecondsRealtime(1);
            director.TriggerMode(3);
            yield return new WaitForSecondsRealtime(4.8f);
            Capture("08_D_Alarm"); Check("Emergency circuit still active", systems.EmergencyAlarm);
            director.ResetToDefault();
            director.TriggerMode(7);
            yield return new WaitForSecondsRealtime(3);
            Capture("09_Whales"); director.ResetToDefault();
            controller.SnapToWall(0);
            presentation.computerBlur = true;
            Capture("10_DesktopBackground"); presentation.computerBlur = false;
            Check("No runtime rendering errors", errors == 0);
            Finish();
        }
        Color32[] Capture(string name, int width = 1600, int height = 900)
        {
            var camera = controller.viewCamera; var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
                Color32[] pixels = image.GetPixels32();
                if (name != null)
                {
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                    int lit = pixels.Count(c => c.r + c.g + c.b > 30);
                    Check("Nonblank frame " + name, lit > pixels.Length * .03f, "visible=" + ((float)lit/pixels.Length).ToString("P1"));
                    int pink = pixels.Count(c => c.r > 220 && c.g < 30 && c.b > 220);
                    Check("No shader-error magenta " + name, pink < pixels.Length * .005f);
                }
                return pixels;
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target); Destroy(image);
            }
        }
        static float Difference(Color32[] a, Color32[] b)
        {
            int changed = 0;
            for (int i = 0; i < a.Length; i++)
                if (Math.Abs(a[i].r-b[i].r) + Math.Abs(a[i].g-b[i].g) + Math.Abs(a[i].b-b[i].b) > 5) changed++;
            return (float)changed / a.Length;
        }
        void Finish()
        {
            if (finished) return; finished = true; Time.timeScale = 1;
            report.Add("RESULT " + (failures == 0 && errors == 0 ? "PASS" : "FAIL") + " failures=" + failures + " errors=" + errors);
            File.WriteAllLines(Path.Combine(output, "art-verification.txt"), report);
            Application.logMessageReceived -= Log; Application.Quit(failures == 0 && errors == 0 ? 0 : 3);
        }
    }
}
