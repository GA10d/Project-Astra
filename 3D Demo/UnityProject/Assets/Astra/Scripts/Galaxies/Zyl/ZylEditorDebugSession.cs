#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    public static class ZylEditorDebugSession
    {
        const string Key="Astra.Zyl.NavigationDebug.";
        public static bool Enabled {get{return SessionState.GetBool(Key+"Enabled",false);}}
        public static string Root
        {
            get
            {
                string root=SessionState.GetString(Key+"Root","");
                string allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"../Library/ZylNavigationDebug/runs"))+Path.DirectorySeparatorChar;
                if(string.IsNullOrEmpty(root)||!Path.GetFullPath(root).StartsWith(allowed,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("调试存档目录无效");
                return root;
            }
        }
        public static void EnableNewRun()
        {
            if(Application.isPlaying)throw new InvalidOperationException("隔离模式必须在进入Play前启用");
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../Library/ZylNavigationDebug/runs",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root); // CabinComputer may persist its first welcome note during Awake.
            SessionState.SetString(Key+"Root",root);SessionState.SetBool(Key+"Enabled",true);
        }
        public static void Disable(){SessionState.SetBool(Key+"Enabled",false);SessionState.EraseString(Key+"Request");}
        public static void Queue(ZylDebugRequest request){if(!Enabled)throw new InvalidOperationException("尚未启用隔离调试");SessionState.SetString(Key+"Request",JsonUtility.ToJson(request));}
        public static ZylDebugRequest TakeRequest(){string text=SessionState.GetString(Key+"Request","");if(string.IsNullOrEmpty(text))return null;SessionState.EraseString(Key+"Request");return JsonUtility.FromJson<ZylDebugRequest>(text);}
    }
}
#endif
