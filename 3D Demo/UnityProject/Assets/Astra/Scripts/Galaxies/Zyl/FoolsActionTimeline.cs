using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AstraCabin.Galaxies.Zyl
{
    [Serializable]
    public sealed class FoolsActionClip
    {
        public string section = "ground";
        public string lineId = "";
        public string pose = "Explain";
        public float offset, duration = 2f, speed = 1f, blend = .62f;
        public string playback = "loop";

        internal MihuoerPose Pose => (MihuoerPose)Enum.Parse(typeof(MihuoerPose), pose);
        internal static void Validate(FoolsActionClip[] clips, bool? seated = null)
        {
            if (clips == null || clips.Length > 64)
                throw new InvalidDataException("米霍尔动作轨需要数组，最多 64 段。");
            foreach (var clip in clips)
            {
                if (clip == null || (seated.HasValue ? clip.section != "dialogue" : clip.section != "ground" && clip.section != "crest") ||
                    (clip.playback != "loop" && clip.playback != "hold") ||
                    !Enum.TryParse(clip.pose, out MihuoerPose pose) || pose.ToString() != clip.pose ||
                    !(seated == true ? IsSeated(pose) : IsStanding(pose)) || !InRange(clip.offset, 0, 3600) || !InRange(clip.duration, .1f, 600) ||
                    !InRange(clip.speed, .25f, 3) || !InRange(clip.blend, 0, 3) ||
                    (clip.lineId != null && clip.lineId.Length > 48))
                    throw new InvalidDataException("米霍尔动作轨含无效的区段、动作或时间参数。");
            }
        }

        static bool IsSeated(MihuoerPose pose) =>
            pose == MihuoerPose.ChairWave || pose == MihuoerPose.ChairTalk ||
            pose == MihuoerPose.ChairListen || pose == MihuoerPose.ChairWorry || pose == MihuoerPose.ChairEmphasis;

        static bool IsStanding(MihuoerPose pose)
        {
            switch (pose)
            {
                case MihuoerPose.Idle: case MihuoerPose.ChinThink: case MihuoerPose.TempleTouch:
                case MihuoerPose.Explain: case MihuoerPose.OpenHands: case MihuoerPose.Emphasis:
                case MihuoerPose.HeadClutch: case MihuoerPose.AngryFists: case MihuoerPose.Slumped:
                case MihuoerPose.KneeBrace: case MihuoerPose.StandWave:
                    return true;
                default: return false;
            }
        }

        static bool InRange(float value, float min, float max) => value >= min && value <= max;
    }

    sealed class FoolsActionTimeline
    {
        readonly MihuoerActionTrack ground;
        readonly MihuoerActionTrack crest;

        public FoolsActionTimeline(ZylPresentationParameters parameters)
        {
            ground = new MihuoerActionTrack(parameters.foolsActionClips, parameters.foolsGroundDialogue, "ground");
            crest = new MihuoerActionTrack(parameters.foolsActionClips, parameters.foolsCrestDialogue, "crest");
        }

        public FoolsActionClip Sample(bool atCrest, float time, out float localTime) =>
            (atCrest ? crest : ground).Sample(time, out localTime);
    }

    sealed class MihuoerActionTrack
    {
        readonly List<(FoolsActionClip clip, float start)> cues = new List<(FoolsActionClip, float)>();

        public MihuoerActionTrack(FoolsActionClip[] clips, MihuoerDialogueLine[] lines, string section = "dialogue")
        {
            foreach (var clip in clips)
            {
                if (clip.section != section) continue;
                float start = 0;
                if (!string.IsNullOrEmpty(clip.lineId))
                {
                    int index = Array.FindIndex(lines, line => line.timerKey == clip.lineId);
                    if (index < 0) continue;
                    for (int i = 0; i < index; i++) start += lines[i].duration;
                }
                cues.Add((clip, start + clip.offset));
            }
        }

        public FoolsActionClip SampleDialogue(MihuoerDialoguePlayer dialogue, out float localTime)
        {
            localTime = 0f;
            return dialogue?.Current == null ? null : Sample(dialogue.ScriptTime, out localTime, dialogue.WaitingForReply);
        }

        public FoolsActionClip Sample(float time, out float localTime, bool waitingForReply = false)
        {
            FoolsActionClip active = null;
            float activeStart = float.NegativeInfinity;
            foreach (var cue in cues)
            {
                // At a reply boundary, evaluate from the preceding line until the player advances.
                float elapsed = time - cue.start;
                bool activeNow = waitingForReply ? elapsed > 0f && elapsed < cue.clip.duration
                    : elapsed >= 0f && elapsed < cue.clip.duration;
                if (cue.start >= activeStart && activeNow)
                {
                    active = cue.clip;
                    activeStart = cue.start;
                }
            }
            localTime = active == null ? 0f : time - activeStart;
            return active;
        }
    }
}
