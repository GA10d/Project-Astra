using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    /// <summary>Single full-display IMGUI layer. Its depth belongs to this component and is never restored to another layer.</summary>
    [DisallowMultipleComponent]
    public sealed class ZylScreenCurtain : MonoBehaviour
    {
        static ZylScreenCurtain instance;
        SetpieceDirector director;
        public int OpaquePaintedFrame {get;private set;}=-1;
        public float Alpha
        {
            get
            {
                if(!director)return 0;
                float encounter=director.encounter&&director.encounter.Active?director.encounter.TransitionAlpha:0;
                var exterior=director.computer&&director.computer.Navigation?director.computer.Navigation.Exterior:null;
                float escape=director.escape&&director.escape.Active?director.escape.TransitionAlpha:0;
                float fools=director.fools&&director.fools.Active?director.fools.TransitionAlpha:0;
                var departure=director.GetComponent<FoolsDeparture>();
                float farewell = director.farewell ? director.farewell.DepartureAlpha : 0;
                return Mathf.Max(farewell,
                    Mathf.Max(Mathf.Max(Mathf.Max(encounter,escape),exterior?exterior.CurtainAlpha:0),
                        Mathf.Max(fools,departure&&departure.Active?departure.Alpha:0)));
            }
        }
        public static bool BlocksInput {get{return instance&&instance.Alpha>0;}}
        public static bool IsOpaque {get{return instance&&instance.Alpha>=1;}}
        public void Initialize(SetpieceDirector owner){director=owner;instance=this;}
        public void ResetPaintConfirmation(){OpaquePaintedFrame=-1;}
        void OnGUI()
        {
            // A separate OnGUI owner prevents invitation/desktop depth restoration from reordering the black frame.
            GUI.depth=-32760;
            float alpha=Alpha;if(alpha<=0)return;
            var matrix=GUI.matrix;var color=GUI.color;var background=GUI.backgroundColor;bool enabled=GUI.enabled;
            try
            {
                GUI.matrix=Matrix4x4.identity;GUI.enabled=true;GUI.backgroundColor=Color.white;
                GUI.color=new Color(0,0,0,alpha);
                GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture,ScaleMode.StretchToFill,true);
                if(alpha>=1&&Event.current.type==EventType.Repaint)OpaquePaintedFrame=Time.frameCount;
                var exterior=director.computer&&director.computer.Navigation?director.computer.Navigation.Exterior:null;
                if(exterior&&!string.IsNullOrEmpty(exterior.Error))
                {
                    GUI.color=Color.white;
                    var style=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=18};
                    GUI.Label(new Rect(30,Screen.height/2-60,Screen.width-60,100),exterior.Error,style);
                    if(GUI.Button(new Rect(30,Screen.height/2+50,180,45),"重试加载"))exterior.RetryLoad();
                }
                if(Event.current.isMouse||Event.current.isKey||Event.current.type==EventType.ScrollWheel)Event.current.Use();
            }
            finally{GUI.matrix=matrix;GUI.color=color;GUI.backgroundColor=background;GUI.enabled=enabled;}
        }
        void OnDestroy(){if(instance==this)instance=null;}
    }
}
