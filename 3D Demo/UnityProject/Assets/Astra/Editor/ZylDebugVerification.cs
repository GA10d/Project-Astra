using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using AstraCabin.Galaxies.Zyl;
namespace AstraCabin.Editor
{
    [InitializeOnLoad]
    static class ZylDebugVerification
    {
        const string Key="Astra.ZylDebugVerify.";
        [Serializable] sealed class HashItem {public string path,hash;}
        [Serializable] sealed class HashList {public List<HashItem> files=new List<HashItem>();}
        static ZylDebugVerification(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
        static string Hash(string file){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file)));}
        static HashList FormalFiles()
        {
            var result=new HashList();string folder=Application.persistentDataPath;
            if(Directory.Exists(folder))foreach(var file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Where(f=>f.Contains(Path.DirectorySeparatorChar+"Navigation"+Path.DirectorySeparatorChar)||Path.GetFileName(f)=="astra-computer-v1.json"))result.files.Add(new HashItem{path=file,hash=Hash(file)});
            return result;
        }
        static void Tick()
        {
            string output=SessionState.GetString(Key+"Output","");
            if(!string.IsNullOrEmpty(output))
            {
                if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>150&&EditorApplication.isPlaying){File.WriteAllText(Path.Combine(output,"timeout.txt"),"专项超过150秒，停止隔离Play。");EditorApplication.isPlaying=false;}
                if(Application.isPlaying&&!SessionState.GetBool(Key+"Spawned",false)&&UnityEngine.Object.FindObjectOfType<NavigationMode>())
                {SessionState.SetBool(Key+"Spawned",true);new GameObject("Zyl Debug Verification | editor only").AddComponent<ZylDebugRuntimeVerification>().Begin(output);}
                return;
            }
            string request=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/ZylDebugVerification.request"));
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            output=File.ReadAllText(request).Trim();File.Delete(request);Directory.CreateDirectory(output);
            if(EditorApplication.isPlayingOrWillChangePlaymode||!UnityEngine.Object.FindObjectOfType<SetpieceDirector>()){File.WriteAllText(Path.Combine(output,"blocked.txt"),"当前不是可进入的舱室编辑状态，未改变Play状态。");return;}
            File.WriteAllText(Path.Combine(output,"formal-before.json"),JsonUtility.ToJson(FormalFiles(),true));
            SessionState.SetString(Key+"Output",output);SessionState.SetFloat(Key+"Start",(float)EditorApplication.timeSinceStartup);SessionState.SetBool(Key+"Spawned",false);
            ZylEditorDebugSession.EnableNewRun();EditorApplication.isPlaying=true;
        }
        static void State(PlayModeStateChange state)
        {
            string output=SessionState.GetString(Key+"Output","");if(state!=PlayModeStateChange.EnteredEditMode||string.IsNullOrEmpty(output))return;
            var before=JsonUtility.FromJson<HashList>(File.ReadAllText(Path.Combine(output,"formal-before.json")));var after=FormalFiles();
            bool same=before.files.Count==after.files.Count&&before.files.All(x=>after.files.Any(y=>y.path==x.path&&y.hash==x.hash));
            File.WriteAllText(Path.Combine(output,"formal-storage-check.txt"),same?"PASS formal navigation and computer files unchanged":"FAIL formal storage changed");
            File.WriteAllText(Path.Combine(output,"editor-finished.txt"),"Returned to Edit Mode; no scene or asset saved.");SessionState.EraseString(Key+"Output");
        }
    }
}
