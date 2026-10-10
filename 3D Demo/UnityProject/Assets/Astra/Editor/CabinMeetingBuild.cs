using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Astra.PerformanceToolV2;
using AstraCabin.Navigation;

public static class CabinMeetingBuild
{
    [MenuItem("Astra/Build meeting demo")]
    public static void Build()
    {
        var campaign=JsonUtility.FromJson<Campaign>(File.ReadAllText("Assets/StreamingAssets/Navigation/campaign.json"));
        var failures=CampaignValidator.Validate(campaign,EventRegistry.CreateDefault());
        foreach(var ev in campaign.events.Where(e=>e.type=="performance"))
        {
            var package=Resources.Load<ToolPackage>("Performances/"+ev.performanceId);
            failures.AddRange(ToolValidation.Package(package));
            if(package&&!package.graph.nodes.Any(n=>n.kind==ToolNodeKind.End))failures.Add("演出包没有 End 节点："+ev.performanceId);
        }
        if(failures.Count>0)throw new InvalidOperationException(string.Join("\n",failures));
        var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var included=graphics.FindProperty("m_AlwaysIncludedShaders");
        foreach(string name in new[]{"Standard","Hidden/Astra/ToolV2/SignalFX"})
        {
            var shader=Shader.Find(name);if(!shader)throw new Exception("Missing shader: "+name);
            bool found=false;for(int i=0;i<included.arraySize;i++)if(included.GetArrayElementAtIndex(i).objectReferenceValue==shader)found=true;
            if(!found){included.InsertArrayElementAtIndex(included.arraySize);included.GetArrayElementAtIndex(included.arraySize-1).objectReferenceValue=shader;}
        }
        graphics.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
        Directory.CreateDirectory("../BuildMeeting");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Astra/Scenes/Cabin04.unity"},locationPathName="../BuildMeeting/Astra Cabin.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
        Debug.Log("ASTRA_MEETING_BUILD_SUCCEEDED "+report.summary.totalSize);
    }
}
