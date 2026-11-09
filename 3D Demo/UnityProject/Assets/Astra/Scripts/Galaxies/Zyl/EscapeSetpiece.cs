using System;
using System.Collections.Generic;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    public sealed class EscapeSetpiece:MonoBehaviour
    {
        public enum Step {WaitingForEnvironment,GravesDialogue,FadeToBattle,BlackBeforeBattle,RevealBattle,Flying,FadeToExit,BlackBeforeExit,RevealCabin,Finished,Error}
        public Step Phase {get;private set;}
        public bool Active {get;private set;}public bool Complete {get;private set;}
        public float TransitionAlpha {get;private set;}public string Error {get;private set;}
        public bool LeavingStomach {get{return leaving;}}
        public EscapeFlight Flight {get;private set;}public Camera Camera {get;private set;}
        public FoolsWalker Walker {get;private set;}public FoolsSlope Ground {get;private set;}
        public Transform GravesRoot {get;private set;}
        public string CurrentText {get{return dialogue?.Current?.text;}}
        public static readonly Vector3 ActorAuth=new Vector3(3.2732f,0,6.9407f),CameraAuth=new Vector3(-2.0167f,0,9.4961f);
        public static readonly Vector3[] GraveAuth={new Vector3(5.7751f,0,6.5558f),new Vector3(4.2910f,0,4.7240f),new Vector3(2.6227f,0,2.7001f)};
        SetpieceDirector director;ZylPresentationParameters p;MihuoerDialoguePlayer dialogue,encouragement;
        float phaseTime;bool leaving,oldUI,captured;Vector3 actorGround,actorFacing;Font font;GUIStyle subtitle;
        // Fitted from the marked ground point in the grave reference view.
        static readonly Vector3 SproutAuth = new Vector3(2.27f, 0f, 6.52f);
        EscapeSprout sprout;
        Transform plantedSprout;
        EscapeGust gust;
        EscapeBackgroundGust backgroundGust;
        float sproutCue, shakeTime;
        Vector3 shotPosition;
        Quaternion shotRotation;
        Vector2 scroll;readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();
        ZylStomachEnvironment Exterior {get{return director.computer.Navigation.Exterior;}}
        ZylScreenCurtain Curtain {get{return director.GetComponent<ZylScreenCurtain>();}}
        CombatMode Combat {get{return director.computer.Combat;}}
        public void Initialize(SetpieceDirector owner){director=owner;}
        public void Show()
        {
            Clear();p=ZylPresentationParameters.Load();Active=true;Phase=Step.WaitingForEnvironment;
            if(!director.computer.Navigation||director.computer.Navigation.ActiveGalaxyId!="zyl"){Fail("逃出生天：请进入 zyl 航程。");return;}
            director.computer.Close();director.controller.SnapToWall(1);director.controller.cinematicWindowLock=true;
        }
        public void Resume()
        {
            if(!Active||Complete)return;if(Phase==Step.Error){Show();return;}
            if(Phase==Step.Flying||Phase==Step.RevealBattle){director.computer.Open();director.computer.OpenApp("combat");}
            else director.computer.Close();
        }
        void Enter(Step next){Phase=next;phaseTime=0;scroll=Vector2.zero;}
        MihuoerDialoguePlayer Dialogue(MihuoerDialogueLine[] lines,string key)
        {
            return new MihuoerDialoguePlayer(lines,(i,action)=>{
                var nav=director.computer.Navigation;var line=lines[i];
                if(nav.Countdown)nav.Countdown.Apply(action,(nav.Session?.Engine.Pending?.id??"escape16")+"/escape/"+key+"/"+(string.IsNullOrEmpty(line.timerKey)?i.ToString():line.timerKey));
            });
        }
        MihuoerPose GravesPose => MihuoerActing.Standing(dialogue,MihuoerPose.ChinThink,MihuoerPose.Idle);
        void BuildShot()
        {
            var exterior=Exterior;var frame=exterior.AuthoredToRuntime;
            Ground=new FoolsSlope(exterior.MihuoerShipAnchor.transform.parent,frame,exterior.MihuoerAnchor.transform.position);
            Walker=new FoolsWalker(exterior.MihuoerActor,Ground);
            var actorMark = Ground.Ground(frame.MultiplyPoint3x4(ActorAuth));
            plantedSprout = exterior.MihuoerShipAnchor.transform.parent.Find(EscapeSprout.PlantedName);
            var sproutSoil = plantedSprout ? plantedSprout.position : Ground.Ground(frame.MultiplyPoint3x4(SproutAuth));
            var towardSprout = Vector3.ProjectOnPlane(sproutSoil - actorMark, Ground.Up).normalized;
            actorGround = Ground.Ground(actorMark + towardSprout * .53f);
            var cameraPosition=Ground.Ground(frame.MultiplyPoint3x4(CameraAuth))+Ground.Up*1.55f;actorFacing=cameraPosition-actorGround;
            var root=new GameObject("Zyl16 | three graves");GravesRoot=root.transform;
            var stone=new Material(Shader.Find("Standard"));stone.name="Zyl16 weathered memorial stone";stone.color=new Color(.28f,.25f,.21f);stone.SetFloat("_Glossiness",.08f);resources.Add(stone);
            EngraveStone(stone);
            var mesh = StoneMesh();
            resources.Add(mesh);
            var soil=new Material(Shader.Find("Standard"));soil.color=new Color(.15f,.11f,.075f);resources.Add(soil);
            for(int i=0;i<3;i++)
            {
                var point=Ground.Ground(frame.MultiplyPoint3x4(GraveAuth[i]));var t=new GameObject("Grave "+(i+1)).transform;t.SetParent(root.transform);t.position=point;t.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(cameraPosition-point,Ground.Up),Ground.Up);t.gameObject.layer=25;
                t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=stone;
                var mound=GameObject.CreatePrimitive(PrimitiveType.Sphere);mound.name="Grave soil";mound.transform.SetParent(t,false);mound.transform.localPosition=new Vector3(0,.025f,.4f);mound.transform.localScale=new Vector3(1.15f,.12f,1.8f);mound.layer=25;Destroy(mound.GetComponent<Collider>());mound.GetComponent<Renderer>().sharedMaterial=soil;
            }
            var cameraObject=new GameObject("Zyl16 | fixed graves camera");Camera=cameraObject.AddComponent<Camera>();Camera.CopyFrom(director.controller.viewCamera);Camera.targetTexture=null;Camera.rect=new Rect(0,0,1,1);Camera.cullingMask=1<<25;Camera.nearClipPlane=.05f;Camera.depth=director.controller.viewCamera.depth+10;Camera.farClipPlane=Mathf.Max(500,Camera.farClipPlane);Camera.clearFlags=CameraClearFlags.SolidColor;
            Camera.transform.position=cameraPosition;Camera.transform.rotation=Quaternion.LookRotation(actorMark+Ground.Up*.95f-cameraPosition,Ground.Up);Camera.fieldOfView=57;
            var light=new GameObject("Graves soft fill").AddComponent<Light>();light.transform.SetParent(root.transform);light.transform.position=cameraPosition+Ground.Up*2;light.type=LightType.Point;light.range=18;light.intensity=1.1f;light.color=new Color(1,.77f,.58f);light.cullingMask=1<<25;
            oldUI=director.controller.showUI;captured=true;director.controller.showUI=false;
            dialogue=Dialogue(p.escapeGravesDialogue,"graves");
            Walker.SampleStandingAt(actorGround,actorFacing,GravesPose,0);
            sprout = new EscapeSprout(GravesRoot, Walker, sproutSoil, actorGround, actorFacing, Ground.Up, resources, plantedSprout, p);
            if(plantedSprout) plantedSprout.gameObject.SetActive(false);
            shotPosition = Camera.transform.position;
            shotRotation = Camera.transform.rotation;
            sproutCue = float.PositiveInfinity;
            shakeTime = float.NegativeInfinity;
            float lineStart = 0f;
            foreach (var line in p.escapeGravesDialogue)
            {
                if (line.pullSprout)
                {
                    sproutCue = lineStart;
                    break;
                }
                lineStart += line.duration;
            }
            if (!float.IsPositiveInfinity(sproutCue))
            {
                var environment = exterior.MihuoerShipAnchor.transform.parent;
                gust = new EscapeGust(GravesRoot, environment, Camera, resources);
                backgroundGust = new EscapeBackgroundGust(environment, Camera, Ground.Up, resources);
            }
        }
        static Mesh StoneMesh()
        {
            // Solid extruded arched headstone; top bevel silhouettes survive the dim stomach lighting.
            var outline=new List<Vector2>{new Vector2(-.45f,0),new Vector2(.45f,0),new Vector2(.45f,.8f)};
            for(int n=0;n<=10;n++){float a=n*Mathf.PI/10;outline.Add(new Vector2(Mathf.Cos(a)*.45f,.8f+Mathf.Sin(a)*.45f));}
            int c=outline.Count;var v=new Vector3[c*2+2];v[c*2]=new Vector3(0,.55f,-.13f);v[c*2+1]=new Vector3(0,.55f,.13f);var tri=new List<int>();
            for(int i=0;i<c;i++){v[i]=new Vector3(outline[i].x,outline[i].y,-.13f);v[c+i]=new Vector3(outline[i].x,outline[i].y,.13f);int j=(i+1)%c;tri.AddRange(new[]{c*2,j,i,c*2+1,c+i,c+j,i,j,c+j,i,c+j,c+i});}
            var uv=new Vector2[v.Length];
            for(int i=0;i<v.Length;i++)
            {
                bool front = v[i].z > 0;
                uv[i] = new Vector2(.5f + (front ? -v[i].x : v[i].x), front ? v[i].y * .8f : v[i].y * .35f);
            }
            var mesh=new Mesh{name="Zyl16 | arched stone"};mesh.vertices=v;mesh.uv=uv;mesh.triangles=tri.ToArray();mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        void EngraveStone(Material stone)
        {
            const int size = 128;
            var lines = new (Vector2 a, Vector2 b)[]
            {
                (new Vector2(.26f,.50f),new Vector2(.26f,.65f)),
                (new Vector2(.26f,.65f),new Vector2(.36f,.65f)),
                (new Vector2(.36f,.65f),new Vector2(.38f,.62f)),
                (new Vector2(.38f,.62f),new Vector2(.36f,.58f)),
                (new Vector2(.36f,.58f),new Vector2(.26f,.58f)),
                (new Vector2(.31f,.58f),new Vector2(.39f,.50f)),
                (new Vector2(.44f,.65f),new Vector2(.55f,.65f)),
                (new Vector2(.495f,.65f),new Vector2(.495f,.50f)),
                (new Vector2(.44f,.50f),new Vector2(.55f,.50f)),
                (new Vector2(.62f,.50f),new Vector2(.62f,.65f)),
                (new Vector2(.62f,.65f),new Vector2(.72f,.65f)),
                (new Vector2(.72f,.65f),new Vector2(.75f,.62f)),
                (new Vector2(.75f,.62f),new Vector2(.72f,.58f)),
                (new Vector2(.72f,.58f),new Vector2(.62f,.58f))
            };
            var heights = new float[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var uv = new Vector2((x + .5f) / size, (y + .5f) / size);
                float distance = float.PositiveInfinity;
                foreach (var line in lines)
                {
                    var stroke = line.b - line.a;
                    var closest = line.a + stroke * Mathf.Clamp01(Vector2.Dot(uv - line.a, stroke) / stroke.sqrMagnitude);
                    distance = Mathf.Min(distance, (uv - closest).sqrMagnitude);
                }
                heights[y * size + x] = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.005f, .016f, Mathf.Sqrt(distance)));
            }
            var grain = new Texture2D(size, size, TextureFormat.RGB24, true) { name = "Memorial stone | engraved RIP grain" };
            var normals = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = "Memorial stone | recessed letter normals" };
            var colors = new Color[size * size];
            var bumps = new Color[size * size];
            var rng = new System.Random(1603);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                float lightness = (.65f + (float)rng.NextDouble() * .35f) * Mathf.Lerp(1f, .32f, heights[i]);
                colors[i] = new Color(lightness, lightness * .97f, lightness * .91f);
                float dx = heights[y * size + Mathf.Min(x + 1, size - 1)] - heights[y * size + Mathf.Max(0, x - 1)];
                float dy = heights[Mathf.Min(y + 1, size - 1) * size + x] - heights[Mathf.Max(0, y - 1) * size + x];
                var normal = new Vector3(dx * .65f, dy * .65f, 1f).normalized * .5f + Vector3.one * .5f;
                bumps[i] = new Color(normal.x, normal.y, normal.z, 1f);
            }
            grain.SetPixels(colors);grain.Apply(true, true);
            normals.SetPixels(bumps);normals.Apply(true, true);
            stone.mainTexture = grain;
            stone.SetTexture("_BumpMap", normals);
            stone.EnableKeyword("_NORMALMAP");
            resources.Add(grain);resources.Add(normals);
        }

        void Update(){Tick(Time.deltaTime);}
        public void Tick(float dt)
        {
            if(captured)director.controller.showUI=oldUI&&director.controller.IsPaused;
            if(!Active||Complete||Phase==Step.Error||director.controller.IsPaused)return;
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0)return;
            bool battle=Phase==Step.Flying;
            if(battle&&(!Combat.IsVisible||Flight.Paused))return;
            if(Phase==Step.GravesDialogue&&director.computer.IsOpen)return;
            try
            {
                phaseTime+=dt;
                switch(Phase)
                {
                    case Step.WaitingForEnvironment:
                        if(Exterior.Error!=null){Fail(Exterior.Error);break;}
                        if(!Exterior.Ready||Exterior.Busy||ZylScreenCurtain.BlocksInput)return;
                        BuildShot();Enter(Step.GravesDialogue);break;
                    case Step.GravesDialogue:
                        dialogue.Tick(dt,false);
                        float age = phaseTime - sproutCue;
                        sprout.Sample(age, GravesPose, dt);
                        shakeTime = age - EscapeSprout.LiftEnd - p.escapeSproutShakeDelay;
                        ShakeGravesCamera(1f);
                        float gustEnd = backgroundGust != null && backgroundGust.Duration > 0f
                            ? p.escapeBackgroundGustDelay + backgroundGust.Duration : .85f;
                        if (dialogue.Ended && (float.IsPositiveInfinity(sproutCue) || shakeTime >= gustEnd))
                        {
                            Curtain.ResetPaintConfirmation();
                            TransitionAlpha = .001f;
                            Enter(Step.FadeToBattle);
                        }
                        break;
                    case Step.FadeToBattle:
                        TransitionAlpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(phaseTime/p.escapeFadeDuration));
                        if (!float.IsNegativeInfinity(shakeTime))
                        {
                            shakeTime += dt;
                            ShakeGravesCamera(1f - TransitionAlpha);
                        }
                        sprout.AdvanceWave(shakeTime + EscapeSprout.LiftEnd + p.escapeSproutShakeDelay, dt);
                        if(phaseTime>=p.escapeFadeDuration){TransitionAlpha=1;Enter(Step.BlackBeforeBattle);}break;
                    case Step.BlackBeforeBattle:
                        if(!Painted()||phaseTime<.15f)return;
                        ReleaseShot();RestoreUI();director.controller.cinematicWindowLock=true;director.controller.ExitWindowView();director.controller.SnapToWall(0);
                        Flight=new EscapeFlight(p.escapeFlightDuration,p.escapeExitDistance,p.escapeHullLoss);Combat.BeginEscapeStory(Flight);encouragement=Dialogue(p.escapeEncouragement,"flight");Combat.EscapeText=encouragement.Current?.text;
                        director.computer.Open();director.computer.OpenApp("combat");Enter(Step.RevealBattle);break;
                    case Step.RevealBattle:
                        TransitionAlpha=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(phaseTime/p.escapeFadeDuration));
                        if(phaseTime>=p.escapeFadeDuration){TransitionAlpha=0;Enter(Step.Flying);}break;
                    case Step.Flying:
                        float playbackDelta = dt * p.escapePlaybackSpeed;
                        Flight.Tick(playbackDelta);encouragement.Tick(playbackDelta,false);if(encouragement.Current!=null)Combat.EscapeText=encouragement.Current.text;
                        if(Flight.Finished){Curtain.ResetPaintConfirmation();TransitionAlpha=.001f;Enter(Step.FadeToExit);}break;
                    case Step.FadeToExit:
                        TransitionAlpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(phaseTime/p.escapeFadeDuration));
                        if(phaseTime>=p.escapeFadeDuration){TransitionAlpha=1;Enter(Step.BlackBeforeExit);}break;
                    case Step.BlackBeforeExit:
                        if(!Painted()||phaseTime<.15f)return;
                        if(!leaving){Combat.EndEscapeStory();director.computer.CloseEscapeCombatApp();director.computer.Close();director.controller.SnapToWall(1);leaving=true;}
                        if(Exterior.Ready||Exterior.Busy||Exterior.CurtainAlpha>0)return;
                        Enter(Step.RevealCabin);break;
                    case Step.RevealCabin:
                        TransitionAlpha=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(phaseTime/p.escapeFadeDuration));
                        if(phaseTime>=p.escapeFadeDuration){TransitionAlpha=0;Complete=true;Enter(Step.Finished);director.controller.cinematicWindowLock=false;director.computer.Open();director.computer.OpenApp("navigation");}break;
                }
            }
            catch(Exception e){Fail("逃出生天："+e.Message);}
        }
        void ShakeGravesCamera(float fade)
        {
            if (shakeTime <= 0f) return;
            float t = shakeTime;
            gust?.Sample(t, Camera.aspect);
            backgroundGust?.Sample(t - p.escapeBackgroundGustDelay);
            float strength = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .24f)) *
                Mathf.Lerp(1f, .38f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - .65f) / 1.45f))) * fade;
            float x = Mathf.Sin(t * 37f) + .4f * Mathf.Sin(t * 19f);
            float y = Mathf.Sin(t * 31f + .7f) - .3f * Mathf.Sin(t * 48f);
            Camera.transform.SetPositionAndRotation(shotPosition + shotRotation * new Vector3(x * .055f, y * .044f, 0f) * strength,
                shotRotation * Quaternion.Euler(y * 1.3f * strength, x * .7f * strength, x * 1.8f * strength));
        }
        bool Painted(){return Curtain&&Curtain.OpaquePaintedFrame>=0&&Time.frameCount>Curtain.OpaquePaintedFrame;}
        void Fail(string error){Error=error;TransitionAlpha=0;Phase=Step.Error;if(Combat)Combat.EndEscapeStory();ReleaseShot();RestoreUI();director.controller.cinematicWindowLock=false;director.controller.ShowNotice(error);}
        void RestoreUI(){if(captured)director.controller.showUI=oldUI;captured=false;}
        void ReleaseShot()
        {
            if(plantedSprout) plantedSprout.gameObject.SetActive(true);
            plantedSprout=null;sprout=null;gust=null;
            backgroundGust?.Dispose();backgroundGust=null;
            if(Camera){Camera.enabled=false;Destroy(Camera.gameObject);}Camera=null;if(Walker!=null)Walker.Dispose();Walker=null;if(Ground!=null)Ground.Dispose();Ground=null;
            if(GravesRoot){GravesRoot.gameObject.SetActive(false);Destroy(GravesRoot.gameObject);}GravesRoot=null;foreach(var r in resources)if(r)Destroy(r);resources.Clear();
        }
        void OnGUI()
        {
            if(!Active||Phase!=Step.GravesDialogue||director.controller.IsPaused||director.computer.IsOpen||string.IsNullOrEmpty(CurrentText)||ZylScreenCurtain.IsOpaque)return;
            if(subtitle==null){font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","Arial"},23);subtitle=new GUIStyle(GUI.skin.label){font=font,fontSize=23,wordWrap=true,richText=false};subtitle.normal.textColor=Color.white;}
            var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;
            try{GUI.matrix=Matrix4x4.identity;GUI.depth=-65;float w=Mathf.Min(1000,Screen.width-64);float th=subtitle.CalcHeight(new GUIContent("米霍尔："+CurrentText),w-48);float h=Mathf.Min(Screen.height*.28f,th+30);var r=new Rect((Screen.width-w)/2,Screen.height-h-40,w,h);GUI.color=new Color(.012f,.018f,.015f,.88f);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;scroll=GUI.BeginScrollView(new Rect(r.x+16,r.y+12,w-32,h-24),scroll,new Rect(0,0,w-48,th));GUI.Label(new Rect(0,0,w-48,th),"米霍尔："+CurrentText,subtitle);GUI.EndScrollView();}
            finally{GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;}
        }
        public void Clear()
        {
            if(director&&Combat)Combat.EndEscapeStory();ReleaseShot();RestoreUI();if(director)director.controller.cinematicWindowLock=false;
            Active=Complete=leaving=false;TransitionAlpha=phaseTime=0;Error=null;Flight=null;dialogue=encouragement=null;Phase=Step.WaitingForEnvironment;
        }
        void OnDisable(){Clear();}void OnDestroy(){Clear();if(font)Destroy(font);}
    }
}
