using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstraCabin.Galaxies.Zyl
{
    enum MihuoerPose
    {
        Idle, ChinThink, TempleTouch, Explain, OpenHands, Emphasis, WalkFlat, WalkUphill,
        ChairWave, ChairTalk, HeadClutch, AngryFists, Slumped, KneeBrace, StandWave,
        ChairListen, ChairWorry, ChairEmphasis
    }

    // Sample on the owner's cinematic clock, including while blending. A pause never advances the pose.
    sealed class MihuoerMotion
    {
        static readonly string[] Clips =
        {
            "01_Idle", "02_ChinThink", "03_TempleTouch", "04_OneHandExplain", "05_OpenHands",
            "06_IndexEmphasis", "07_WalkFlat", "08_WalkUphill", "S2_01_ChairWave", "S2_02_ChairTalk",
            "S2_03_HeadClutch", "S2_04_AngryFists", "S2_05_Slumped", "S2_06_KneeBrace",
            "S2_08_StandWave", "ChairListen", "ChairWorry", "ChairEmphasis"
        };
        readonly float blendDuration;
        readonly Animation animation;
        readonly Dictionary<MihuoerPose, Track> tracks = new Dictionary<MihuoerPose, Track>();
        Track target;
        float transition;

        sealed class Track
        {
            public MihuoerPose Pose;
            public AnimationState State;
            public float Time;
            public float StartWeight;
        }

        public MihuoerMotion(Animation animation, float blendDuration = .62f)
        {
            this.animation = animation;
            this.blendDuration = blendDuration;
            Reset();
        }

        public void Reset()
        {
            animation.Stop();
            target = null;
            animation.enabled = false;
        }

        public void Sample(MihuoerPose pose, float dt, float gaitPhase = -1f)
        {
            if (!tracks.TryGetValue(pose, out var requested))
            {
                string name = Clips[(int)pose];
                var clip = Resources.Load<AnimationClip>("Galaxies/Zyl/MihuoerMotions/" + name);
                if (!clip) throw new InvalidOperationException("米霍尔动作资源缺失：" + name);
                if (!animation.GetClip(name)) animation.AddClip(clip, name);
                requested = new Track { Pose = pose, State = animation[name] };
                tracks.Add(pose, requested);
            }

            if (requested != target)
            {
                foreach (var track in tracks.Values)
                    track.StartWeight = track.State.enabled ? track.State.weight : 0f;
                if (requested.StartWeight <= 0f) requested.Time = 0f;
                transition = target == null ? blendDuration : 0f;
                target = requested;
            }

            transition += dt;
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(transition / blendDuration));
            foreach (var track in tracks.Values)
            {
                var state = track.State;
                state.weight = Mathf.Lerp(track.StartWeight, track == target ? 1f : 0f, alpha);
                state.enabled = state.weight > 0f;
                if (!state.enabled) continue;
                state.speed = 0f;
                if (gaitPhase >= 0f && (track.Pose == MihuoerPose.WalkFlat || track.Pose == MihuoerPose.WalkUphill))
                    track.Time = gaitPhase * state.length;
                else
                    track.Time += dt;

                bool once = track.Pose == MihuoerPose.StandWave || track.Pose == MihuoerPose.AngryFists
                    || track.Pose == MihuoerPose.Emphasis || track.Pose == MihuoerPose.KneeBrace;
                state.wrapMode = once ? WrapMode.ClampForever : WrapMode.Loop;
                state.time = once ? Mathf.Min(track.Time, state.length) : Mathf.Repeat(track.Time, state.length);
            }
            animation.Sample();
        }
    }
}
