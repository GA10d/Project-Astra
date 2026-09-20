using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstraCabin
{
    public enum CabinSound { Click, Switch, Lever, Hatch, Valve, Printer, Radio, Turn, Hum }

    /// <summary>Original synthesized cabin Foley. No downloaded recordings or music.</summary>
    public class CabinAudio : MonoBehaviour
    {
        public static CabinAudio Instance { get; private set; }
        public bool muted;
        [Range(0f, 1f)] public float volume = 0.65f;
        [Range(4, 24)] public int maximumVoices = 12;
        public bool ambientEnabled = true;
        const int SampleRate = 22050;
        readonly Dictionary<CabinSound, AudioClip> clips = new Dictionary<CabinSound, AudioClip>();
        readonly List<AudioSource> voices = new List<AudioSource>();
        AudioSource ambient;
        int playSequence;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            foreach (CabinSound sound in Enum.GetValues(typeof(CabinSound)))
                clips[sound] = Synthesize(sound);
            ambient = gameObject.AddComponent<AudioSource>();
            ambient.clip = clips[CabinSound.Hum];
            ambient.loop = true;
            ambient.spatialBlend = 0f;
            ambient.volume = volume * 0.32f;
            ambient.mute = muted || !ambientEnabled;
            ambient.Play();
        }

        void Update()
        {
            if (ambient != null)
            {
                ambient.mute = muted || !ambientEnabled;
                ambient.volume = volume * 0.32f;
            }
            for (int i = voices.Count - 1; i >= 0; --i)
            {
                if (voices[i] == null) { voices.RemoveAt(i); continue; }
                voices[i].mute = muted;
                if (!voices[i].isPlaying)
                {
                    Destroy(voices[i].gameObject);
                    voices.RemoveAt(i);
                }
            }
        }

        public void SetMuted(bool value)
        {
            muted = value;
            if (ambient != null) ambient.mute = muted || !ambientEnabled;
            foreach (AudioSource voice in voices) if (voice != null) voice.mute = muted;
        }

        public void SetAmbientEnabled(bool value) { ambientEnabled = value; }

        public void Play(CabinSound kind, Vector3 position)
        {
            if (muted || !clips.TryGetValue(kind, out AudioClip clip)) return;
            // Hum is the continuous shared room tone; do not stack a second loop.
            if (kind == CabinSound.Hum) { SetAmbientEnabled(true); return; }
            while (voices.Count >= Mathf.Max(1, maximumVoices))
            {
                if (voices[0] != null) Destroy(voices[0].gameObject);
                voices.RemoveAt(0);
            }
            GameObject node = new GameObject("Foley_" + kind);
            node.transform.SetParent(transform, false);
            node.transform.position = position;
            AudioSource source = node.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = kind == CabinSound.Turn ? 0f : 0.65f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 0.65f;
            source.maxDistance = 7f;
            source.dopplerLevel = 0f;
            source.pitch = 0.985f + ((playSequence++ * 37) % 11) * 0.003f;
            source.volume = volume * (kind == CabinSound.Turn ? 0.38f : 0.80f);
            source.Play();
            voices.Add(source);
            Destroy(node, clip.length / source.pitch + 0.15f);
        }

        public AudioClip GetClip(CabinSound kind)
        {
            clips.TryGetValue(kind, out AudioClip clip);
            return clip;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (AudioClip clip in clips.Values) if (clip != null) Destroy(clip);
            clips.Clear();
        }

        static AudioClip Synthesize(CabinSound kind)
        {
            float duration;
            switch (kind)
            {
                case CabinSound.Click: duration = 0.11f; break;
                case CabinSound.Switch: duration = 0.20f; break;
                case CabinSound.Lever: duration = 0.64f; break;
                case CabinSound.Hatch: duration = 0.85f; break;
                case CabinSound.Valve: duration = 0.90f; break;
                case CabinSound.Printer: duration = 1.20f; break;
                case CabinSound.Radio: duration = 0.82f; break;
                case CabinSound.Turn: duration = 0.32f; break;
                default: duration = 6f; break;
            }
            int length = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[length];
            uint rng = 17339u + (uint)kind * 1747u;
            float filtered = 0f;
            const float Tau = Mathf.PI * 2f;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float u = t / duration;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                float noise = (rng & 65535u) / 32767.5f - 1f;
                filtered = Mathf.Lerp(filtered, noise, 0.075f);
                float v = 0f;
                switch (kind)
                {
                    case CabinSound.Click:
                        v = Impact(t, 1050f, 65f, noise) * 0.34f + Impact(t-0.026f, 580f, 95f, noise)*0.12f;
                        break;
                    case CabinSound.Switch:
                        v = Impact(t, 1350f, 78f, noise)*0.28f + Impact(t-0.065f, 490f, 52f, noise)*0.24f;
                        break;
                    case CabinSound.Lever:
                        v = filtered*Mathf.Sin(Mathf.PI*u)*0.25f
                            + Mathf.Sin(Tau*(180f*t+55f*t*t))*Mathf.Sin(Mathf.PI*u)*0.035f
                            + Impact(t-0.06f,410f,38f,noise)*0.17f + Impact(t-0.47f,260f,28f,noise)*0.34f;
                        break;
                    case CabinSound.Hatch:
                        v = filtered*Mathf.Sin(Mathf.PI*u)*0.18f
                            + Impact(t-0.11f,120f,12f,noise)*0.33f
                            + Impact(t-0.53f,74f,11f,noise)*0.48f
                            + Mathf.Sin(Tau*188f*t)*Mathf.Exp(-t*9f)*0.055f;
                        break;
                    case CabinSound.Valve:
                        v = (filtered*0.30f + Mathf.Sin(Tau*(230f*t+17f*Mathf.Sin(t*4f)))*0.026f)
                            *Mathf.Sin(Mathf.PI*u)
                            + Impact(t-0.67f,330f,32f,noise)*0.18f;
                        break;
                    case CabinSound.Printer:
                        float gate = 0.35f+0.65f*Mathf.SmoothStep(0,1,Mathf.Sin(t*15f)*0.5f+0.5f);
                        v = (Mathf.Sin(Tau*(125f*t+16f*Mathf.Sin(t*3f))) * 0.06f
                            + Mathf.Sin(Tau*392f*t)*0.018f + filtered*0.10f) * gate*Mathf.Sin(Mathf.PI*u)
                            + Impact(t-0.03f,560f,52f,noise)*0.15f;
                        break;
                    case CabinSound.Radio:
                        float burst = Mathf.Pow(Mathf.Sin(t*17f)*0.5f+0.5f,3f);
                        v = (noise*0.065f*burst + filtered*0.17f
                            + Mathf.Sin(Tau*(780f*t+35f*t*t))*0.035f*Mathf.Sin(t*32f))*Mathf.Sin(Mathf.PI*u);
                        break;
                    case CabinSound.Turn:
                        v = filtered*0.20f*Mathf.Sin(Mathf.PI*u)
                            + Mathf.Sin(Tau*95f*t)*0.008f*Mathf.Sin(Mathf.PI*u);
                        break;
                    case CabinSound.Hum:
                        // Integer-period partials make a seamless six-second loop.
                        float wind = Mathf.Sin(Tau*7f*t/6f)*Mathf.Sin(Tau*19f*t/6f);
                        v = Mathf.Sin(Tau*48f*t)*0.10f + Mathf.Sin(Tau*96f*t)*0.023f
                            + Mathf.Sin(Tau*144f*t)*0.012f
                            + Mathf.Sin(Tau*317f*t)*0.006f*(0.6f+wind*0.4f);
                        break;
                }
                if (kind != CabinSound.Hum)
                    v *= Mathf.Clamp01(t*900f) * Mathf.Clamp01((duration-t)*55f);
                samples[i] = Mathf.Clamp(v, -0.85f, 0.85f);
            }
            AudioClip result = AudioClip.Create("Astra_Original_" + kind, length, 1, SampleRate, false);
            result.SetData(samples, 0);
            return result;
        }

        static float Impact(float t, float frequency, float decay, float noise)
        {
            if (t < 0f) return 0f;
            return (Mathf.Sin(2f*Mathf.PI*frequency*t)*0.63f + noise*0.37f)*Mathf.Exp(-t*decay);
        }
    }
}
