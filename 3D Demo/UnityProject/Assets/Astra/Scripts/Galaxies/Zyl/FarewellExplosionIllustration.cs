using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstraCabin.Galaxies.Zyl
{
    sealed class FarewellExplosionIllustration : IDisposable
    {
        const string ResourceRoot = "Galaxies/Zyl/Farewell/";
        const float LastFrameHold = .16f;
        const float SingleFrameHold = .25f;
        internal const int FrameCount = 6;
        static readonly float[] AuthoredTimes = { 0, .14f, .38f, .74f, 1.15f, 1.50f };
        static readonly float[] V2FrameDurations = { .16f, .10f, .10f, .26f, .34f, .40f };
        internal float Duration { get; }
        internal Texture2D FullFrame { get; private set; }

        // Artwork-space bounds, ordered as the four foreground rocks and the two central remnants.
        // The central pair is still covered by the flash in the first drawing.
        static readonly Vector4[] OpeningRocks =
        {
            ArtBounds(305, 610, 582, 905), ArtBounds(1362, 265, 1562, 439),
            ArtBounds(562, 204, 699, 345), ArtBounds(1306, 748, 1459, 916),
            Vector4.zero, Vector4.zero
        };
        static readonly Vector4[] DebrisRocks =
        {
            ArtBounds(267, 603, 575, 922), ArtBounds(1413, 208, 1633, 406),
            ArtBounds(521, 164, 673, 325), ArtBounds(1341, 740, 1497, 919),
            ArtBounds(741, 375, 931, 568), ArtBounds(983, 390, 1206, 655)
        };

        static Vector4 ArtBounds(int left, int top, int right, int bottom) =>
            new Vector4((left + right) / 3840f, 1 - (top + bottom) / 2160f,
                (right - left) / 3840f, (bottom - top) / 2160f);
        static readonly int FrameTexture = Shader.PropertyToID("_Frame");

        readonly Texture2D[] frames;
        readonly float[] times;
        readonly bool includesOpeningFrame;
        readonly Material material;
        readonly Transform plate;
        readonly Transform[] rocks;
        readonly Vector3[][] rockVertices;
        readonly Vector4[] projectedRocks = new Vector4[DebrisRocks.Length];
        static readonly int RockTargets = Shader.PropertyToID("_RockTargets");
        static readonly int FrameRocks = Shader.PropertyToID("_FrameRocks");
        static readonly int OpeningFit = Shader.PropertyToID("_OpeningFit");
        static readonly int DebrisFit = Shader.PropertyToID("_DebrisFit");

        internal static FarewellExplosionIllustration Load(Transform parent, int layer, Transform[] rocks, bool[] selected, bool fullFrame = false)
        {
            if (!Array.Exists(selected, value => value)) return null;
            Shader shader = null;
            if (!fullFrame)
            {
                shader = Resources.Load<Shader>(ResourceRoot + "FarewellIllustration");
                if (!shader || !shader.isSupported) return null;
            }
            string folder = fullFrame ? "ExplosionIllustrationV2/" : "ExplosionIllustration/";
            var frames = new List<Texture2D>(FrameCount);
            var times = new List<float>(FrameCount);
            float nextTime = 0;
            for (int i = 0; i < FrameCount; i++)
            {
                if (!selected[i]) continue;
                var frame = Resources.Load<Texture2D>(ResourceRoot + folder + "Frame" + (i + 1));
                if (!frame) return null;
                frames.Add(frame);
                times.Add(nextTime);
                // Keep the outgoing beat of each selected drawing and skip omitted beats.
                nextTime += fullFrame ? V2FrameDurations[i] :
                    (i + 1 < FrameCount ? AuthoredTimes[i + 1] - AuthoredTimes[i] : LastFrameHold);
            }
            return new FarewellExplosionIllustration(parent, layer, shader, frames.ToArray(), times.ToArray(), selected[0], rocks, nextTime, fullFrame);
        }

        FarewellExplosionIllustration(Transform parent, int layer, Shader shader, Texture2D[] frames,
            float[] times, bool includesOpeningFrame, Transform[] rocks, float duration, bool fullFrame)
        {
            this.frames = frames;
            this.times = times;
            this.includesOpeningFrame = includesOpeningFrame;
            Duration = !fullFrame && frames.Length == 1 ? SingleFrameHold : duration;
            // V2 paints its own window, so it uses neither a world plate nor silhouette warping.
            if (fullFrame) return;
            this.rocks = rocks;
            rockVertices = new Vector3[rocks.Length][];
            for (int i = 0; i < rocks.Length; i++)
                rockVertices[i] = rocks[i].GetComponent<MeshFilter>().sharedMesh.vertices;
            material = new Material(shader) { name = "Farewell | explosion illustration" };
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Farewell | exterior illustration plate";
            go.SetActive(false);
            go.layer = layer;
            plate = go.transform;
            plate.SetParent(parent, false);
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            UnityEngine.Object.Destroy(collider);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        internal void Begin(CabinController controller)
        {
            if (!plate) { Sample(0); return; }
            // The plate is beyond the real window and in front of all explosion geometry.
            // Calibrate its size to the artwork handoff framing from the final observation pose.
            const float depth = 8;
            const float frameFraction = .78f;
            float halfHeight = depth * Mathf.Tan(controller.windowFieldOfView * .5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * (16f / 9f);
            var rotation = Quaternion.Euler(0, 90, 0);
            var offset = new Vector3(.007f * halfWidth, .027f * halfHeight, depth);
            plate.SetPositionAndRotation(controller.windowObservationPosition + rotation * offset, rotation);
            plate.localScale = new Vector3(2 * halfWidth * frameFraction, 2 * halfHeight * frameFraction, 1);
            AlignRocks(controller.viewCamera.transform.position);
            Sample(0);
            plate.gameObject.SetActive(true);
        }

        internal void AlignRocks(Vector3 eyePosition)
        {
            if (!plate) return;
            Matrix4x4 worldToPlate = plate.worldToLocalMatrix;
            Vector3 eye = worldToPlate.MultiplyPoint3x4(eyePosition);
            for (int i = 0; i < rocks.Length; i++)
            {
                Matrix4x4 meshToPlate = worldToPlate * rocks[i].localToWorldMatrix;
                var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                foreach (var vertex in rockVertices[i])
                {
                    Vector3 point = meshToPlate.MultiplyPoint3x4(vertex);
                    if (point.z <= 0) continue;
                    // Intersect the eye-to-rock ray with the illustration plane in UV space.
                    float t = -eye.z / (point.z - eye.z);
                    Vector2 uv = Vector2.one * .5f + (Vector2)Vector3.LerpUnclamped(eye, point, t);
                    min = Vector2.Min(min, uv);
                    max = Vector2.Max(max, uv);
                }
                Vector2 center = (min + max) * .5f;
                Vector2 halfSize = (max - min) * .5f;
                projectedRocks[i] = halfSize.x > 0 && halfSize.y > 0
                    ? new Vector4(center.x, center.y, halfSize.x, halfSize.y)
                    : Vector4.zero;
            }
            material.SetVectorArray(RockTargets, projectedRocks);
            material.SetVector(OpeningFit, FitPlate(OpeningRocks));
            material.SetVector(DebrisFit, FitPlate(DebrisRocks));
        }

        Vector4 FitPlate(Vector4[] source)
        {
            // Fit the whole starfield to the shared motion before matching individual silhouettes.
            const int foregroundCount = 4;
            Vector2 sourceMean = Vector2.zero, targetMean = Vector2.zero;
            for (int i = 0; i < foregroundCount; i++)
            {
                if (projectedRocks[i].z <= 0) return new Vector4(0, 0, 1, 1);
                sourceMean += (Vector2)source[i] / foregroundCount;
                targetMean += (Vector2)projectedRocks[i] / foregroundCount;
            }
            Vector2 variance = Vector2.zero, covariance = Vector2.zero;
            for (int i = 0; i < foregroundCount; i++)
            {
                Vector2 from = (Vector2)source[i] - sourceMean;
                Vector2 to = (Vector2)projectedRocks[i] - targetMean;
                variance += Vector2.Scale(from, from);
                covariance += Vector2.Scale(from, to);
            }
            var scale = new Vector2(Mathf.Clamp(covariance.x / variance.x, .86f, 1.12f),
                Mathf.Clamp(covariance.y / variance.y, .86f, 1.12f));
            Vector2 offset = targetMean - Vector2.Scale(scale, sourceMean);
            return new Vector4(Mathf.Clamp(offset.x, -.08f, .08f), Mathf.Clamp(offset.y, -.08f, .08f),
                scale.x, scale.y);
        }

        internal void Sample(float time)
        {
            int index = 0;
            while (index + 1 < times.Length && time >= times[index + 1]) index++;
            if (!material) { FullFrame = frames[index]; return; }
            bool openingFrame = includesOpeningFrame && index == 0;
            material.SetTexture(FrameTexture, frames[index]);
            material.SetVectorArray(FrameRocks, openingFrame ? OpeningRocks : DebrisRocks);
            material.SetFloat("_OpeningFrame", openingFrame ? 1 : 0);
        }

        public void Dispose()
        {
            if (plate)
            {
                plate.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(plate.gameObject);
            }
            if (material) UnityEngine.Object.Destroy(material);
        }
    }
}
