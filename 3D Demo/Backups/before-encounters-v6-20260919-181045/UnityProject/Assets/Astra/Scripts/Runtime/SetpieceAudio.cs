using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstraCabin
{
    public sealed class SetpieceAudio : MonoBehaviour
    {
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        readonly List<AudioSource> voices=new List<AudioSource>();
        AudioSource alarm; CabinController controller; bool paused;
        public bool AlarmPlaying { get { return alarm && alarm.isPlaying; } }
        void Awake()
        {
            controller=GetComponent<CabinController>();
            foreach(string name in new[]{"impact","explosion","laser","ufo","engine","warp","monster","hammer","success","alert","alarm"})
                clips[name]=Synthesize(name);
            alarm=gameObject.AddComponent<AudioSource>(); alarm.clip=clips["alarm"]; alarm.loop=true;
            alarm.playOnAwake=false; alarm.spatialBlend=0; alarm.volume=.19f;
        }
        void Update()
        {
            bool muted=CabinAudio.Instance && CabinAudio.Instance.muted;
            float volume=CabinAudio.Instance?CabinAudio.Instance.volume:.65f;
            alarm.mute=muted; alarm.volume=.24f*volume;
            foreach(var voice in voices) if(voice) voice.mute=muted;
            bool now=controller && controller.IsPaused;
            if(now!=paused)
            {
                if(now) { alarm.Pause(); foreach(var voice in voices) if(voice) voice.Pause(); }
                else { alarm.UnPause(); foreach(var voice in voices) if(voice) voice.UnPause(); }
                paused=now;
            }
        }
        public void SetAlarm(bool enabled)
        {
            if(enabled) { if(!alarm.isPlaying) alarm.Play(); } else alarm.Stop();
        }
        public void Play(string name,float volume=1f)
        {
            if(!clips.TryGetValue(name,out var clip)) clip=clips["impact"];
            AudioSource voice=null;
            foreach(var candidate in voices) if(!candidate.isPlaying) { voice=candidate; break; }
            if(!voice && voices.Count<12)
            {
                voice=gameObject.AddComponent<AudioSource>(); voice.playOnAwake=false; voice.spatialBlend=0; voices.Add(voice);
            }
            if(!voice) return;
            voice.clip=clip; voice.volume=Mathf.Clamp01(volume)*.45f*(CabinAudio.Instance?CabinAudio.Instance.volume:.65f);
            voice.mute=CabinAudio.Instance && CabinAudio.Instance.muted; voice.pitch=1; voice.Play();
        }
        public void StopAll()
        {
            if(alarm) alarm.Stop(); foreach(var voice in voices) if(voice) voice.Stop();
        }
        static AudioClip Synthesize(string name)
        {
            const int rate=22050;
            float duration=name=="alarm"?2f:name=="warp"?2.2f:name=="monster"?2.1f:name=="laser"?.22f:name=="hammer"?.28f:name=="alert"?.42f:1.25f;
            var data=new float[Mathf.CeilToInt(duration*rate)]; var random=new System.Random(1943+name.Length*17);
            float filtered=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate,u=t/duration,noise=(float)random.NextDouble()*2-1;
                filtered=Mathf.Lerp(filtered,noise,.14f);
                float value;
                switch(name)
                {
                    case "alarm":
                        float f=780+240*Mathf.Sin(t*Mathf.PI*2);
                        value=.45f*Mathf.Sin(2*Mathf.PI*f*t)+.14f*Mathf.Sin(4*Mathf.PI*f*t); break;
                    case "laser": value=Mathf.Sin(2*Mathf.PI*(2100*t-3400*t*t))*.7f*Mathf.Exp(-u*7); break;
                    case "warp": value=(filtered*.8f+Mathf.Sin(2*Mathf.PI*(90*t+250*t*t))*.18f)*Mathf.Sin(u*Mathf.PI); break;
                    case "monster": value=(Mathf.Sin(t*145)+.28f*Mathf.Sin(t*217)+filtered*.3f)*.5f*Mathf.Sin(u*Mathf.PI); break;
                    case "ufo": value=Mathf.Sin(t*1400+Mathf.Sin(t*18)*6)*.35f*Mathf.Sin(u*Mathf.PI); break;
                    case "engine": value=(filtered+Mathf.Sin(t*150)*.25f)*Mathf.Sin(u*Mathf.PI); break;
                    case "success": value=Mathf.Sin(t*(u<.33f?2600:u<.66f?3200:3900))*.25f*Mathf.Sin(u*Mathf.PI); break;
                    case "alert": value=Mathf.Sin(t*4800)*.4f*Mathf.Pow(Mathf.Sin(u*Mathf.PI*3),2); break;
                    case "hammer": value=(noise*.65f+Mathf.Sin(t*980)*.4f)*Mathf.Exp(-u*13); break;
                    default: value=(filtered*2.8f+Mathf.Sin(2*Mathf.PI*(52*t-12*t*t))*.42f)*Mathf.Exp(-u*5); break;
                }
                float fade=name=="alarm"?1:Mathf.Min(1,t/.004f)*Mathf.Min(1,(duration-t)/.025f);
                data[i]=Mathf.Clamp(value*fade,-.85f,.85f);
            }
            var clip=AudioClip.Create("Original synthesized "+name,data.Length,1,rate,false); clip.SetData(data,0); return clip;
        }
        void OnDestroy() { foreach(var clip in clips.Values) if(clip) Destroy(clip); }
    }
}
