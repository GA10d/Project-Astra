using System;
using System.IO;
namespace AstraCabin.Galaxies.Zyl
{
    [Serializable]
    public sealed class MihuoerDialogueLine
    {
        public string text="";
        public float duration=5f;
        public string[] replies=new string[0];
        public static void Validate(MihuoerDialogueLine[] lines)
        {
            if(lines==null)throw new InvalidDataException("freeDialogue需要数组");
            foreach(var line in lines)
            {
                if(line==null)throw new InvalidDataException("对话段落不能为null");
                CheckText(line.text,1000);
                if(float.IsNaN(line.duration)||float.IsInfinity(line.duration)||line.duration<.1f||line.duration>600f)throw new InvalidDataException("台词持续时间必须在0.1–600秒之间");
                if(line.replies==null)line.replies=new string[0];
                foreach(var reply in line.replies)CheckText(reply,160);
            }
        }
        static void CheckText(string value,int max)
        {
            if(string.IsNullOrWhiteSpace(value)||value.Length>max)throw new InvalidDataException("台词/回复为空或超过长度限制");
            foreach(char c in value)if(char.IsControl(c)&&c!='\n'&&c!='\r'&&c!='\t')throw new InvalidDataException("台词/回复含不支持的控制字符");
        }
    }

    // Only this clock waits for player input. The caller's beast/StoryTime clock continues independently.
    public sealed class MihuoerDialoguePlayer
    {
        readonly MihuoerDialogueLine[] lines;
        public int Index {get;private set;}
        public float Elapsed {get;private set;}
        public bool Interrupted {get;private set;}
        public bool Ended {get{return Index>=lines.Length;}}
        public MihuoerDialogueLine Current {get{return Ended||Interrupted?null:lines[Index];}}
        public bool WaitingForReply {get{return Current!=null&&Elapsed>=Current.duration&&Current.replies.Length>0;}}
        public MihuoerDialoguePlayer(MihuoerDialogueLine[] source){MihuoerDialogueLine.Validate(source);lines=source;}
        public void Tick(float dt,bool interrupt)
        {
            // Distance takes priority over every line timer, empty/end state and outstanding choice.
            if(interrupt){Interrupted=true;return;}
            if(Interrupted||Ended||dt<=0||float.IsNaN(dt)||float.IsInfinity(dt))return;
            float remaining=dt;
            while(!Ended)
            {
                var line=lines[Index];
                float consumed=Math.Min(remaining,Math.Max(0,line.duration-Elapsed));
                Elapsed+=consumed;remaining-=consumed;
                if(Elapsed<line.duration||line.replies.Length>0)return;
                Index++;Elapsed=0;
                if(remaining<=0)return;
            }
        }
        public bool Choose(int option)
        {
            if(!WaitingForReply||option<0||option>=Current.replies.Length)return false;
            Index++;Elapsed=0;return true;
        }
    }
}
