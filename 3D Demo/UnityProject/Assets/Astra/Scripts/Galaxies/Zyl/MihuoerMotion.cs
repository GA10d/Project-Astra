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
        readonly Dictionary<(MihuoerPose pose, FoolsActionClip cue), Track> tracks = new Dictionary<(MihuoerPose, FoolsActionClip), Track>();
        readonly List<(MihuoerPose pose, FoolsActionClip cue)> expired = new List<(MihuoerPose, FoolsActionClip)>();
        Track target;
        float transition, transitionDuration;
        int nextCueId;
        internal MihuoerPose? CurrentPose => target?.Pose;

        sealed class Track
        {
            public MihuoerPose Pose;
            public AnimationState State;
            public float Time;
            public float StartWeight;
            public float ReplyPlaybackTime;
            public FoolsActionClip Cue;
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
            foreach (var track in tracks.Values)
                if (track.Cue != null) animation.RemoveClip(track.State.name);
            tracks.Clear();
            nextCueId=0;
            target = null;
            animation.enabled = false;
        }

        public void Sample(MihuoerPose pose, float dt, float gaitPhase = -1f, FoolsActionClip cue = null, float cueTime = 0f, bool waitingForReply = false)
        {
            if (waitingForReply && target != null)
            {
                // Advance the pose the player last saw while the dialogue clock waits.
                if (target.Cue != null) target.ReplyPlaybackTime += dt;
                Continue(dt);
                return;
            }

            var key = (pose, cue);
            if (!tracks.TryGetValue(key, out var requested))
            {
                string name = Clips[(int)pose];
                var clip = Resources.Load<AnimationClip>("Galaxies/Zyl/MihuoerMotions/" + name);
                if (!clip) throw new InvalidOperationException("米霍尔动作资源缺失：" + name);
                string stateName = cue == null ? name : name + "_foolsCue_" + nextCueId++;
                if (!animation.GetClip(stateName)) animation.AddClip(clip, stateName);
                requested = new Track { Pose = pose, State = animation[stateName], Cue = cue };
                tracks.Add(key, requested);
            }

            if (requested != target)
            {
                foreach (var track in tracks.Values)
                    track.StartWeight = track.State.enabled ? track.State.weight : 0f;
                if (requested.StartWeight <= 0f) requested.Time = 0f;
                transitionDuration = cue?.blend ?? target?.Cue?.blend ?? blendDuration;
                transition = target == null ? transitionDuration : 0f;
                requested.ReplyPlaybackTime = 0f;
                target = requested;
            }

            Advance(dt, gaitPhase, cue != null ? (float?)cueTime : null);
        }

        public void Continue(float dt) => Advance(dt);

        void Advance(float dt, float gaitPhase = -1f, float? cueTime = null)
        {
            transition += dt;
            float alpha = transitionDuration <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(transition / transitionDuration));
            foreach (var entry in tracks)
            {
                var track = entry.Value;
                var state = track.State;
                state.weight = Mathf.Lerp(track.StartWeight, track == target ? 1f : 0f, alpha);
                state.enabled = state.weight > 0f;
                if (!state.enabled)
                {
                    if (track != target && track.Cue != null) expired.Add(entry.Key);
                    continue;
                }
                state.speed = 0f;
                if (track == target && cueTime.HasValue)
                    track.Time = (cueTime.Value + track.ReplyPlaybackTime) * track.Cue.speed;
                else if (gaitPhase >= 0f && (track.Pose == MihuoerPose.WalkFlat || track.Pose == MihuoerPose.WalkUphill))
                    track.Time = gaitPhase * state.length;
                else
                    track.Time += dt * (track.Cue?.speed ?? 1f);

                bool once = track.Cue != null ? track.Cue.playback == "hold" :
                    track.Pose == MihuoerPose.StandWave || track.Pose == MihuoerPose.AngryFists ||
                    track.Pose == MihuoerPose.Emphasis || track.Pose == MihuoerPose.KneeBrace;
                state.wrapMode = once ? WrapMode.ClampForever : WrapMode.Loop;
                state.time = once ? Mathf.Min(track.Time, state.length) : Mathf.Repeat(track.Time, state.length);
            }
            foreach (var expiredKey in expired)
            {
                animation.RemoveClip(tracks[expiredKey].State.name);
                tracks.Remove(expiredKey);
            }
            expired.Clear();
            animation.Sample();
        }
    }
}
