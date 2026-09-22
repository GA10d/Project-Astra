using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AstraCabin
{
    // Explicit opt-in regression run; never touches a player's saved desktop.
    public sealed class CabinRenderingVerification : MonoBehaviour
    {
        readonly List<string> report = new List<string>();
        string output;
        int failures, errors;
        CabinController controller;
        CabinPresentation presentation;
        CabinLightBudget budget;
        Camera view;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--astra-render-qa") < 0) return;
            var cabin = FindObjectOfType<CabinController>();
            if (cabin) cabin.gameObject.AddComponent<CabinRenderingVerification>();
        }
        void Check(string name, bool pass, string detail = "")
        {
            report.Add((pass ? "PASS " : "FAIL ") + name + " | " + detail);
            if (!pass) failures++;
        }
        void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors++; report.Add("ERROR " + message + "\n" + stack);
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--astra-render-qa");
            if (index < 0 || index + 1 >= args.Length) yield break;
            output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output);
            Application.logMessageReceived += Log;
            Application.runInBackground = true; Application.targetFrameRate = 60;
            controller = GetComponent<CabinController>(); controller.automationMode = true; controller.showUI = false;
            GetComponent<SetpieceDirector>().automationMode = true;
            view = controller.viewCamera; presentation = view.GetComponent<CabinPresentation>();
            budget = view.GetComponent<CabinLightBudget>();
            yield return new WaitForSecondsRealtime(1);
            controller.SetPaused(true);
            for (int wall = 0; wall < 4; wall++)
            {
                controller.SnapToWall(wall);
                view.transform.rotation = Quaternion.Euler(0, wall * 90f - .001f, 0);
                var left = Capture(null, 800, 450);
                string shadows = Shadows();
                view.transform.rotation = Quaternion.Euler(0, wall * 90f + .001f, 0);
                var right = Capture(null, 800, 450);
                float changed = Difference(left, right);
                Check("No lighting jump across center at wall " + wall, changed < .01f, "changed=" + changed.ToString("P3"));
                bool stable = shadows == Shadows(); int maxShadows = 0;
                foreach (float yaw in new[] { -4f, -3f, -1f, 0f, 1f, 3f, 4f, -4f })
                {
                    view.transform.rotation = Quaternion.Euler(0, wall * 90f + yaw, 0);
                    Capture(null, 320, 180);
                    stable &= Shadows() == shadows;
                    maxShadows = Mathf.Max(maxShadows, budget.keyLights.Count(l => l && l.shadows != LightShadows.None));
                }
                Check("Mouse sweep keeps wall shadows stable " + wall, stable);
                Check("Two shadow-map budget at wall " + wall, maxShadows <= 2, "lights=" + maxShadows);
            }
            controller.SetPaused(false);
            foreach (int direction in new[] { 1, -1 })
            {
                bool continuous = true, withinBudget = true;
                int peak = 0;
                for (int turn = 0; turn < 4; turn++)
                {
                    controller.Turn(direction);
                    float yaw = controller.CurrentYaw;
                    float[] strengths = budget.keyLights.Select(l => l.shadowStrength).ToArray();
                    while (controller.IsTurning)
                    {
                        yield return null; Capture(null, 320, 180);
                        float allowed = Mathf.Abs(Mathf.DeltaAngle(yaw, controller.CurrentYaw)) * Mathf.Deg2Rad * 5f + .015f;
                        for (int i = 0; i < strengths.Length; i++)
                        {
                            continuous &= Mathf.Abs(budget.keyLights[i].shadowStrength - strengths[i]) <= allowed;
                            strengths[i] = budget.keyLights[i].shadowStrength;
                        }
                        int count = budget.keyLights.Count(l => l.shadows != LightShadows.None);
                        peak = Mathf.Max(peak, count); withinBudget &= count <= 2;
                        yaw = controller.CurrentYaw;
                    }
                }
                Check("Continuous shadow fade during turns " + direction, continuous);
                Check("Two-shadow budget through full turn cycle " + direction, withinBudget && peak == 2, "peak=" + peak);
            }
            controller.SetPaused(true);
            controller.SnapToWall(0);
            // Diagnose the HDR scene before tone mapping or background blur.
            presentation.enabled = false;
            Capture("01_RawHDR", 1600, 900, true);
            for (int wall = 1; wall < 4; wall++)
            {
                controller.SnapToWall(wall); report.Add("HDR wall " + wall);
                Capture(null, 1600, 900, true);
            }
            controller.SnapToWall(0);
            presentation.enabled = true;
            Capture("02_Cabin", 1600, 900);
            view.transform.rotation = Quaternion.Euler(0,-3,0); Capture("02_Cabin_Left",1600,900);
            view.transform.rotation = Quaternion.Euler(0,3,0); Capture("02_Cabin_Right",1600,900);
            controller.SnapToWall(0);
            presentation.computerBlur = true;
            foreach (var size in new[] { new Vector2Int(1280,720), new Vector2Int(1600,900), new Vector2Int(1920,1080) })
            {
                var pixels = Capture("03_Blur_" + size.x, size.x, size.y);
                // These side regions contain illuminated paint, knobs and gauges.
                // Pure-black plateaus here are the reported blur corruption.
                int black = 0;
                for (int y = size.y / 5; y < size.y / 3; y++)
                    for (int x = 0; x < size.x; x++)
                        if (x < size.x / 9 || x > size.x * 8 / 9)
                        {
                            var c = pixels[y * size.x + x];
                            if (c.r == 0 && c.g == 0 && c.b == 0) black++;
                        }
                Check("No black blur blocks at " + size.x, black == 0, "black pixels=" + black);
            }
            presentation.computerBlur = false; controller.SetPaused(false);
            var computer = GetComponent<CabinComputer>();
            Check("Isolated desktop storage", computer.IsTestStorage);
            Check("Desktop opens", computer.Open()); computer.OpenApp("browser");
            if (Array.IndexOf(args, "--capture-desktop") >= 0)
            {
                Screen.SetResolution(1600,900,false);
                yield return new WaitForSecondsRealtime(.6f);
                yield return new WaitForEndOfFrame();
                var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, "04_Desktop.png"), image.EncodeToPNG()); Destroy(image);
            }
            computer.Close();
            Check("No runtime errors", errors == 0);
            report.Add("RESULT " + (failures == 0 && errors == 0 ? "PASS" : "FAIL") + " failures=" + failures + " errors=" + errors);
            File.WriteAllLines(Path.Combine(output, "render-verification.txt"), report);
            Application.logMessageReceived -= Log; Time.timeScale = 1;
            Application.Quit(failures == 0 && errors == 0 ? 0 : 3);
        }
        string Shadows() { return string.Join(",", budget.keyLights.Where(l => l && l.shadows != LightShadows.None).Select(l => l.name)); }
        Color32[] Capture(string name, int width, int height, bool hdr = false)
        {
            var previous = view.targetTexture; var active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 24, hdr ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGB32, hdr ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            var image = new Texture2D(width, height, hdr ? TextureFormat.RGBAFloat : TextureFormat.RGB24, false, hdr);
            try
            {
                view.targetTexture = target; view.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
                if (hdr)
                {
                    var floats = image.GetPixels();
                    int invalid = floats.Count(c => !Finite(c.r) || !Finite(c.g) || !Finite(c.b));
                    Check("Finite HDR scene before blur", invalid == 0, "nonfinite pixels=" + invalid);
                }
                var pixels = image.GetPixels32();
                if (name != null)
                {
                    var png = new Texture2D(width,height,TextureFormat.RGB24,false);
                    png.SetPixels32(pixels); png.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), png.EncodeToPNG()); Destroy(png);
                }
                return pixels;
            }
            finally { view.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); Destroy(image); }
        }
        static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
        static float Difference(Color32[] a, Color32[] b)
        {
            int changed = 0;
            for (int i=0;i<a.Length;i++) if (Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>12) changed++;
            return (float)changed/a.Length;
        }
    }
}
