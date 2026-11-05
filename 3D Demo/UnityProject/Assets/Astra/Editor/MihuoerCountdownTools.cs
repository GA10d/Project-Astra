using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AstraCabin.Galaxies.Zyl;
namespace AstraCabin.Editor
{
    [CustomEditor(typeof(MihuoerCountdown))]
    sealed class MihuoerCountdownInspector : UnityEditor.Editor
    {
        float value=300;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var timer=(MihuoerCountdown)target;
            EditorGUILayout.HelpBox("场景、演出、对话共用同一个计时器。等待回复/电脑界面继续计时；Esc全局暂停冻结。退出不扣离线时间。冻结显示不暂停实际计时。",MessageType.Info);
            using(new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                var clock=Application.isPlaying?timer.Clock:null;
                EditorGUILayout.LabelField("实际剩余",clock==null?"未绑定zyl航程":MihuoerCountdownClock.Format(clock.State.remaining));
                if(clock!=null)EditorGUILayout.LabelField("状态",(clock.State.running?"运行":"暂停")+" / "+(clock.State.visible?"可见":"隐藏")+(clock.State.displayFrozen?" / 显示冻结":""));
                value=EditorGUILayout.Slider("操作时间（秒）",value,0,5999);EditorGUILayout.LabelField("分:秒",MihuoerCountdownClock.Format(value));
                EditorGUILayout.BeginHorizontal();if(GUILayout.Button("设置"))timer.SetRemainingSeconds(value);if(GUILayout.Button("设置并开始"))timer.SetAndStart(value);if(GUILayout.Button("暂停"))timer.PauseCountdown();if(GUILayout.Button("继续"))timer.ResumeCountdown();EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();if(GUILayout.Button("增加"))timer.AddSeconds(value);if(GUILayout.Button("减少"))timer.SubtractSeconds(value);if(GUILayout.Button("显示"))timer.ShowCountdown();if(GUILayout.Button("隐藏"))timer.HideCountdown();EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();if(GUILayout.Button("冻结显示"))timer.FreezeDisplayAt(value);if(GUILayout.Button("解除显示冻结"))timer.UnfreezeDisplay();EditorGUILayout.EndHorizontal();
            }
            if(!string.IsNullOrEmpty(timer.Error))EditorGUILayout.HelpBox(timer.Error,MessageType.Error);
        }
    }
    [CustomEditor(typeof(MihuoerCountdownCue))]
    sealed class MihuoerCountdownCueInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();var action=serializedObject.FindProperty("action");var kind=action.FindPropertyRelative("kind");var seconds=action.FindPropertyRelative("seconds");
            int current=Math.Max(0,Array.IndexOf(CountdownAction.Kinds,kind.stringValue));int selected=EditorGUILayout.Popup("计时操作",current,CountdownAction.Kinds);kind.stringValue=CountdownAction.Kinds[selected];
            if(CountdownAction.NeedsSeconds(kind.stringValue))seconds.doubleValue=EditorGUILayout.Slider("秒数",(float)seconds.doubleValue,0,5999);
            serializedObject.ApplyModifiedProperties();EditorGUILayout.HelpBox("将场景UnityEvent或Timeline信号连接到此组件Execute；动画事件也可调用Execute。每次触发均执行，对话的一次性操作则在HTML配置。",MessageType.Info);
            using(new EditorGUI.DisabledScope(!Application.isPlaying))if(GUILayout.Button("执行一次"))((MihuoerCountdownCue)target).Execute();
        }
    }
    public sealed class MihuoerCountdownPreview : EditorWindow
    {
        PreviewRenderUtility preview;
        GameObject actor;
        MihuoerCountdownSkin skin;
        float seconds=300,yaw;
        [MenuItem("Astra/Zyl/预览米霍尔皮下倒计时")]
        public static void Open(){GetWindow<MihuoerCountdownPreview>("米霍尔 · 皮下倒计时").minSize=new Vector2(560,480);}
        [MenuItem("Astra/Zyl/选中当前倒计时控制器")]
        public static void SelectTimer(){var timer=FindObjectOfType<MihuoerCountdown>();if(timer)Selection.activeGameObject=timer.gameObject;else Debug.Log("进入Play并打开zyl航程后可选中统一倒计时控制器。");}
        void Create()
        {
            preview=new PreviewRenderUtility();preview.camera.fieldOfView=32;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=20;
            var prefab=Resources.Load<GameObject>("Galaxies/Zyl/Encounter/Mihuoer");if(!prefab)return;
            actor=Instantiate(prefab);preview.AddSingleGO(actor);skin=MihuoerCountdownSkin.Attach(actor,null,ZylPresentationParameters.Load());
            var animation=actor.GetComponentInChildren<Animation>();if(animation&&animation.GetClip("SeatedTalk")){animation.enabled=false;animation.GetClip("SeatedTalk").SampleAnimation(animation.gameObject,0);}
            preview.lights[0].intensity=.8f;preview.lights[0].transform.rotation=Quaternion.Euler(25,40,0);preview.lights[1].intensity=.45f;preview.lights[1].transform.rotation=Quaternion.Euler(340,220,0);preview.ambientColor=new Color(.25f,.25f,.25f);
        }
        void OnGUI()
        {
            if(preview==null)Create();EditorGUILayout.HelpBox("原模型实例与独立材质预览，不修改资源或场景。皮下数字随原网格运动，保持正常遮挡。",MessageType.None);
            seconds=EditorGUILayout.Slider("显示时间（秒）",seconds,0,5999);yaw=EditorGUILayout.Slider("观察角度",yaw,-180,180);
            EditorGUILayout.LabelField("读数",MihuoerCountdownClock.Format(seconds));if(GUILayout.Button("重读参数")){OnDisable();Create();}
            if(!actor||!skin){EditorGUILayout.HelpBox("缺少米霍尔模型或Shader。",MessageType.Error);return;}
            skin.SetPreview(seconds);actor.transform.rotation=Quaternion.Euler(0,yaw,0);
            var rect=GUILayoutUtility.GetRect(100,100,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));if(Event.current.type!=EventType.Repaint)return;
            preview.BeginPreview(rect,GUIStyle.none);preview.camera.transform.position=new Vector3(0,.8f,.78f);preview.camera.transform.LookAt(new Vector3(0,.8f,0));preview.camera.aspect=rect.width/Mathf.Max(1,rect.height);preview.camera.Render();var texture=preview.EndPreview();GUI.DrawTexture(rect,texture,ScaleMode.ScaleToFit,false);
        }
        void OnDisable(){if(preview!=null)preview.Cleanup();preview=null;actor=null;skin=null;}
    }
    sealed class MihuoerCountdownTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture(){if(!assetPath.EndsWith("/Countdown/CountdownWoundMask.png",StringComparison.Ordinal))return;var importer=(TextureImporter)assetImporter;importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;importer.sRGBTexture=false;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;}
    }
}

