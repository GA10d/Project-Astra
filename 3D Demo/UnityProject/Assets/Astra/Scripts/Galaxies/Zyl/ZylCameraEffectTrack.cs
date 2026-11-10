using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AstraCabin.Galaxies.Zyl
{
    [Serializable]
    public sealed class ZylCameraEffect
    {
        public string section = "dialogue";
        public string lineId = "";
        public float offset, duration = 1f, strength = 35f;

        internal static void Validate(ZylCameraEffect[] effects, params string[] sections)
        {
            if (effects == null || effects.Length > 64)
                throw new InvalidDataException("镜头效果轨需要数组，最多 64 段。");
            foreach (var effect in effects)
            {
                if (effect == null || Array.IndexOf(sections, effect.section) < 0 ||
                    !InRange(effect.offset, 0, 3600) || !InRange(effect.duration, .1f, 600) ||
                    !InRange(effect.strength, 0, 100) || (effect.lineId?.Length ?? 0) > 48)
                    throw new InvalidDataException("镜头效果轨含无效的区段、时间或震动强度。");
            }
        }

        static bool InRange(float value, float min, float max) => value >= min && value <= max;
    }

    sealed class ZylCameraEffectTrack
    {
        readonly List<(ZylCameraEffect effect, float start)> cues = new List<(ZylCameraEffect, float)>();
        Camera camera;
        Quaternion originalRotation;
        Vector3 euler;
        bool applied;

        internal void Bind(ZylCameraEffect[] effects, MihuoerDialogueLine[] lines, string section)
        {
            Clear();
            cues.Clear();
            foreach (var effect in effects)
            {
                if (effect.section != section || effect.strength == 0f) continue;
                float start = effect.offset;
                if (!string.IsNullOrEmpty(effect.lineId))
                {
                    int line = Array.FindIndex(lines, item => item.timerKey == effect.lineId);
                    if (line < 0) continue;
                    for (int i = 0; i < line; i++) start += lines[i].duration;
                }
                cues.Add((effect, start));
            }
        }

        internal void SampleDialogue(Camera target, MihuoerDialoguePlayer dialogue)
        {
            if (dialogue?.Current == null) Clear();
            else Sample(target, dialogue.ScriptTime);
        }

        internal void Sample(Camera target, float time)
        {
            Vector3 sum = Vector3.zero;
            foreach (var cue in cues)
            {
                float age = time - cue.start;
                float duration = cue.effect.duration;
                if (age <= 0f || age >= duration) continue;
                float fade = Mathf.Min(.16f, duration * .25f);
                float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Min(age, duration - age) / fade);
                sum += new Vector3(Mathf.Sin(age * 49f), Mathf.Sin(age * 39f) * .8f,
                    Mathf.Sin(age * 55f) * .4f) * (envelope * cue.effect.strength * .03f);
            }
            if (!target || sum.sqrMagnitude < 1e-8f)
            {
                Clear();
                return;
            }
            if (camera != target)
            {
                Clear();
                camera = target;
                Camera.onPreCull += BeforeRender;
                Camera.onPostRender += AfterRender;
            }
            euler = Vector3.ClampMagnitude(sum, 3f);
        }

        // Compose with the current shot only during rendering. The director keeps ownership
        // of the base camera pose, including any shake already built into the scene.
        void BeforeRender(Camera rendering)
        {
            if (rendering != camera || applied) return;
            originalRotation = camera.transform.rotation;
            camera.transform.rotation = originalRotation * Quaternion.Euler(euler);
            applied = true;
        }

        void AfterRender(Camera rendering)
        {
            if (rendering == camera) Restore();
        }

        void Restore()
        {
            if (applied && camera) camera.transform.rotation = originalRotation;
            applied = false;
        }

        internal void Clear()
        {
            if (ReferenceEquals(camera, null)) return;
            Camera.onPreCull -= BeforeRender;
            Camera.onPostRender -= AfterRender;
            Restore();
            camera = null;
        }
    }
}
