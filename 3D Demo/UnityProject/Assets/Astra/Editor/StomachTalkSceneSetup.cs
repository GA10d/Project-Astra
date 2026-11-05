using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using AstraCabin.Galaxies.Zyl;
namespace AstraCabin.Editor
{
    public static class StomachTalkSceneSetup
    {
        [InitializeOnLoadMethod] static void AfterReload(){EditorApplication.delayCall+=AfterImport;}
        static void AfterImport(){if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=AfterImport;return;}Ensure(false,0);}
        [MenuItem("Astra/Zyl/创建或定位肚中会谈米霍尔锚点")]
        public static void EnsureSceneAnchor(){Ensure(true,0);}
        [MenuItem("Astra/Zyl/创建或定位肚中会谈飞船锚点")]
        public static void EnsureShipAnchor(){Ensure(true,1);}
        [MenuItem("Astra/Zyl/创建或定位肚中会谈太空舱锚点")]
        public static void EnsureCabinAnchor(){Ensure(true,2);}
        static void Ensure(bool select,int kind)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)return;
            var scene=SceneManager.GetSceneByPath(ZylStomachEnvironment.ScenePath);bool opened=!(scene.IsValid()&&scene.isLoaded);
            if(!opened&&scene.isDirty){Debug.LogWarning("胃内场景有未保存修改；请保存后使用Astra/Zyl菜单创建米霍尔锚点。初始停靠锚点不会被修改。");return;}
            var active=SceneManager.GetActiveScene();
            try
            {
                if(opened)scene=EditorSceneManager.OpenScene(ZylStomachEnvironment.ScenePath,OpenSceneMode.Additive);
                var settings=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<StomachEnvironmentSettings>(true)).FirstOrDefault();
                if(!settings)throw new InvalidOperationException("找不到胃内初始停靠锚点。");
                var existing=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<StomachMihuoerAnchor>(true)).FirstOrDefault();
                var existingShip=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<StomachMihuoerShipAnchor>(true)).FirstOrDefault();
                var existingCabin=settings.GetComponent<StomachCabinAnchor>();
                var position=settings.transform.position;var rotation=settings.transform.rotation;
                var cabin=StomachCabinAnchor.Ensure(settings);var character=StomachMihuoerAnchor.Ensure(settings);var craft=StomachMihuoerShipAnchor.Ensure(settings,character);
                if(settings.transform.position!=position||settings.transform.rotation!=rotation)throw new InvalidOperationException("停靠锚点被意外改动，已中止保存。");
                if(!existing||!existingShip||!existingCabin){EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("米霍尔锚点保存失败。");Debug.Log("STOMACH_CHARACTER_AND_SHIP_ANCHORS_READY; existing authored transforms preserved");}
                if(select)Selection.activeGameObject=kind==2?cabin.gameObject:kind==1?craft.gameObject:character.gameObject;
            }
            catch(Exception e){Debug.LogError("肚中会谈锚点准备失败："+e.Message);}
            finally{if(opened&&scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}
        }
    }
    [CustomEditor(typeof(StomachMihuoerAnchor))]
    sealed class StomachMihuoerAnchorInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI(){base.OnInspectorGUI();EditorGUILayout.HelpBox("Transform位置是米霍尔角色模型根节点，蓝色Z轴是角色面向方向。角色与飞船独立摆放，移动此锚点不会移动飞船。请在编辑模式摆放并保存胃内场景；下次载入时使用此姿态。太空舱原停靠锚点不受影响。",MessageType.Info);}
    }
    [CustomEditor(typeof(StomachMihuoerShipAnchor))]
    sealed class StomachMihuoerShipAnchorInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI(){base.OnInspectorGUI();EditorGUILayout.HelpBox("Transform控制米霍尔飞船的中心位置和根节点旋转，与米霍尔角色锚点独立。蓝色Z轴表示飞船根节点前向，请按模型朝向调整。修改后保存胃内场景，下次载入生效。",MessageType.Info);}
    }
    [CustomEditor(typeof(StomachCabinAnchor))]
    sealed class StomachCabinAnchorInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI(){base.OnInspectorGUI();EditorGUILayout.HelpBox("这是原有太空舱停靠锚点的可视标记，绿色线框示意舱室，红色本地+X箭头表示舷窗朝向。修改此物体Transform调整初始停靠位置与朝向；StomachEnvironmentSettings仍负责环境设置。角色与飞船使用各自独立锚点。",MessageType.Info);}
    }
}
