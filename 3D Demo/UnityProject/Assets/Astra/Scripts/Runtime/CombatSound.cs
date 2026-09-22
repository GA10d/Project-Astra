using System.Collections.Generic;
using UnityEngine;

namespace AstraCabin
{
    // Own voices prevent a paused combat app from stopping unrelated cabin setpiece sounds.
    public sealed class CombatSound : MonoBehaviour
    {
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> voices = new List<AudioSource>();
        AudioSource engine, music;
        bool active, localMuted;
        float musicEnvelope;
        public const float MusicFadeInSeconds = 2.5f, MusicFadeOutSeconds = .65f;
        public float MusicGain { get { return music ? music.volume : 0; } }
        public bool MusicPlaying { get { return music && music.isPlaying; } }
        public bool MusicLoaded { get { return music && music.clip; } }
        void Awake()
        {
            foreach (string name in new[] { "shot", "hit", "kill", "damage", "boost", "missile", "lock", "sector", "clear", "heat", "lost", "engine" })
                clips[name] = MakeClip(name);
            engine = gameObject.AddComponent<AudioSource>(); engine.playOnAwake = false; engine.spatialBlend = 0; engine.loop = true; engine.clip = clips["engine"];
            music = gameObject.AddComponent<AudioSource>(); music.playOnAwake = false; music.spatialBlend = 0; music.loop = true;
            music.clip = Resources.Load<AudioClip>("Computer/CombatMusic"); music.volume = 0;
        }
        public void BeginSortie()
        {
            if (!music) return;
            music.Stop(); music.time = 0; musicEnvelope = 0; music.volume = 0;
        }
        void Update()
        {
            if (!music || !music.clip) return;
            if (active && !music.isPlaying) { music.UnPause(); if (!music.isPlaying) music.Play(); }
            musicEnvelope = Mathf.MoveTowards(musicEnvelope, active ? 1 : 0,
                Time.unscaledDeltaTime / (active ? MusicFadeInSeconds : MusicFadeOutSeconds));
            music.volume = Mathf.SmoothStep(0, .46f, musicEnvelope) * Master;
            music.mute = localMuted || (CabinAudio.Instance && CabinAudio.Instance.muted);
            if (!active && musicEnvelope == 0 && music.isPlaying) music.Pause();
        }
        public void SetActive(bool value, float thrust, bool mute)
        {
            localMuted = mute;
            if (!engine) return;
            if (!value) { if (active) { engine.Stop(); foreach (var voice in voices) voice.Stop(); } active = false; return; }
            active = true; if (!engine.isPlaying) engine.Play();
            engine.pitch = .7f + thrust * .7f; engine.volume = .045f * Master;
            engine.mute = mute || (CabinAudio.Instance && CabinAudio.Instance.muted);
            foreach (var voice in voices) voice.mute = engine.mute;
        }
        float Master { get { return CabinAudio.Instance ? CabinAudio.Instance.volume : .65f; } }
        public void Play(string name, float volume, bool mute)
        {
            if (mute || (CabinAudio.Instance && CabinAudio.Instance.muted) || !clips.ContainsKey(name)) return;
            AudioSource voice = null;
            foreach (var v in voices) if (!v.isPlaying) { voice = v; break; }
            if (!voice && voices.Count < 14) { voice = gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false; voice.spatialBlend = 0; voices.Add(voice); }
            if (!voice) return;
            voice.clip = clips[name]; voice.volume = volume * .36f * Master; voice.pitch = 1; voice.mute = false; voice.Play();
        }
        static AudioClip MakeClip(string name)
        {
            const int rate = 22050;
            float duration = name == "engine" ? 1 : name == "shot" ? .1f : name == "hit" ? .09f : name == "damage" ? .65f : .45f;
            var data = new float[Mathf.CeilToInt(duration * rate)]; var rng = new System.Random(1943); float noise = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate, u = t / duration;
                noise = Mathf.Lerp(noise, (float)rng.NextDouble() * 2 - 1, .16f);
                float tone;
                switch (name)
                {
                    case "engine": tone = Mathf.Sin(t * Mathf.PI * 2 * 70) * .4f + Mathf.Sin(t * Mathf.PI * 2 * 140) * .15f; break;
                    case "shot": tone = (Mathf.Sin((1900 * t - 7200 * t * t) * 6.28f) * .55f + noise) * Mathf.Exp(-u * 8); break;
                    case "hit": tone = noise * Mathf.Exp(-u * 12); break;
                    case "kill": case "lost": tone = (noise * 2 + Mathf.Sin(t * 320) * .55f) * Mathf.Exp(-u * 5); break;
                    case "damage": tone = (noise * Mathf.Exp(-u * 20) + Mathf.Sin(t * 4900) * Mathf.Pow(Mathf.Sin(u * Mathf.PI * 3), 8) * .45f); break;
                    case "boost": case "missile": tone = (noise + Mathf.Sin((100 * t + 180 * t * t) * 6.28f) * .2f) * Mathf.Sin(u * Mathf.PI); break;
                    default: tone = Mathf.Sin(t * (u < .5f ? 3200 : 4100)) * .3f * Mathf.Sin(u * Mathf.PI); break;
                }
                data[i] = Mathf.Clamp(tone, -.9f, .9f) * (name == "engine" ? 1 : Mathf.Min(1, t / .004f) * Mathf.Min(1, (duration - t) / .02f));
            }
            AudioClip clip = AudioClip.Create("Astra combat " + name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        void OnDisable() { active = false; musicEnvelope = 0; if (music) music.Stop(); if (engine) engine.Stop(); foreach (var v in voices) if (v) v.Stop(); }
        void OnDestroy() { foreach (var c in clips.Values) if (c) Destroy(c); }
    }
}
