using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AstraCabin;
using AstraCabin.Ships;

public static class AstraShipPrefabTools
{
    public const string PrefabPath="Assets/Astra/Prefabs/Ships/AstraPlayerShip.prefab";
    [MenuItem("Astra/Ships/Build reusable ship prefab")]
    public static void BuildPrefab()
    {
        // Read an owned temporary copy of the disk scene. Never alter an open authoring scene.
        var previousActive=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var stagingPath=AssetDatabase.GenerateUniqueAssetPath("Assets/Astra/Editor/Ships/__ShipSource.unity");
        if(!AssetDatabase.CopyAsset("Assets/Astra/Scenes/Cabin04.unity",stagingPath))throw new Exception("Cannot copy authored cabin source scene");
        var source=EditorSceneManager.OpenScene(stagingPath,OpenSceneMode.Additive);UnityEngine.SceneManagement.SceneManager.SetActiveScene(source);
        try {
        var roots=source.GetRootGameObjects();var root=new GameObject("ASTRA | Interactive player ship");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,source);
        foreach(var child in roots)child.transform.SetParent(root.transform,true);
        var controller=root.GetComponentInChildren<CabinController>(true);var director=root.GetComponentInChildren<SetpieceDirector>(true);
        var rig=root.AddComponent<AstraShipRig>();rig.systemsRoot=controller.gameObject;rig.cockpitCamera=controller.viewCamera;
        if(director.planet)UnityEngine.Object.DestroyImmediate(director.planet.gameObject);if(director.stars)UnityEngine.Object.DestroyImmediate(director.stars.gameObject);
        director.planet=null;director.stars=null;director.allowDebugHotkeys=false;director.discoverExteriorAutomatically=false;
        rig.hull=roots.First(g=>g&&g.name.StartsWith("Astra Cabin")).transform;
        rig.cockpitCamera.farClipPlane=1800;rig.cockpitCamera.clearFlags=CameraClearFlags.Skybox;
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        ValidateAsset(root);
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        if(!prefab)throw new Exception("Ship prefab save failed");
        AssetDatabase.SaveAssets();
        ValidateAsset(prefab);Selection.activeObject=prefab;
        } finally { EditorSceneManager.CloseScene(source,true);if(previousActive.IsValid()&&previousActive.isLoaded)UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousActive);AssetDatabase.DeleteAsset(stagingPath); }
    }
    static void ValidateAsset(GameObject prefab)
    {
        var rig=prefab.GetComponent<AstraShipRig>();if(!rig||!rig.systemsRoot||!rig.hull||!rig.cockpitCamera)throw new Exception("Ship bindings missing");
        if(prefab.GetComponentsInChildren<DistantPlanet>(true).Length!=0||prefab.GetComponentsInChildren<LoopingStarfield>(true).Length!=0)throw new Exception("Unexpected built-in space background");
        if(prefab.GetComponentsInChildren<SetpieceDirector>(true).Any(d=>d.allowDebugHotkeys||d.discoverExteriorAutomatically))throw new Exception("Debug setpieces enabled");
        if(prefab.GetComponentsInChildren<CabinInteractable>(true).Length!=9)throw new Exception("Expected nine authored controls");
        foreach(var t in prefab.GetComponentsInChildren<Transform>(true))
        {
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)!=0)throw new Exception("Missing ship script: "+t.name);
            foreach(var component in t.GetComponents<Component>().Where(c=>c))
            {
                var so=new SerializedObject(component);var property=so.GetIterator();
                while(property.Next(true))
                {
                    if(property.propertyType!=SerializedPropertyType.ObjectReference)continue;
                    var target=property.objectReferenceValue;Transform linked=null;
                    if(target is Component c)linked=c.transform;else if(target is GameObject g)linked=g.transform;
                    if(linked&&!EditorUtility.IsPersistent(target)&&!linked.IsChildOf(prefab.transform))throw new Exception("Scene reference escaped prefab: "+t.name+"/"+property.propertyPath);
                }
            }
        }
        Debug.Log("ASTRA_SHIP_PREFAB_VALID | nine controls, complete internal references, no missing scripts");
    }
    [MenuItem("Astra/Ships/Place interactive player ship")]
    public static void Place()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);if(!prefab){Debug.LogError("Build the reusable ship prefab first.");return;}
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);Undo.RegisterCreatedObjectUndo(instance,"Place ASTRA ship");Selection.activeGameObject=instance;EditorSceneManager.MarkSceneDirty(instance.scene);
    }
    [MenuItem("Astra/Ships/Replace selected static cabin")]
    public static void ReplaceStaticCabin()
    {
        var selected=Selection.activeGameObject;var old=selected?PrefabUtility.GetNearestPrefabInstanceRoot(selected):null;
        if(!old||PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old)!="Assets/Astra/Models/Astra_Cabin.fbx"||old.GetComponentsInChildren<CabinInteractable>(true).Length!=0)
        {Debug.LogError("Select an unconfigured static Astra_Cabin FBX instance to replace.");return;}
        if((old.transform.lossyScale-Vector3.one).sqrMagnitude>.00001f){Debug.LogError("Restore the static cabin scale to (1,1,1) before replacement.");return;}
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);if(!prefab)return;
        Undo.IncrementCurrentGroup();var group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Replace static cabin with interactive ship");
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,old.scene);Undo.RegisterCreatedObjectUndo(instance,"Create interactive ship");
        Undo.RecordObject(instance.transform,"Keep cabin placement");instance.transform.SetParent(old.transform.parent,true);instance.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);
        Undo.DestroyObjectImmediate(old);Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);Selection.activeGameObject=instance;EditorSceneManager.MarkSceneDirty(instance.scene);
    }
    [MenuItem("Astra/Ships/Validate ships in current scene")]
    public static void ValidateScene()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var rigs=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AstraShipRig>(true)).ToArray();
        var players=rigs.Where(r=>r.gameObject.activeInHierarchy&&r.enabled&&r.usage==AstraShipUsage.PlayerShip).ToArray();
        if(players.Length!=1){Debug.LogWarning("ASTRA: expected one active PlayerShip, found "+players.Length+". Other ships should use Scenery.");return;}
        if((players[0].transform.lossyScale-Vector3.one).sqrMagnitude>.00001f){Debug.LogError("ASTRA: player ship scale must be (1,1,1), including its parents.");return;}
        if(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CabinController>(true)).Any(c=>!c.GetComponentInParent<AstraShipRig>()))
        {Debug.LogError("ASTRA: remove the other standalone cabin controller before play.");return;}
        Debug.Log("ASTRA: ship placement is valid. Other scene cameras/listeners will be disabled when Play starts.");
    }
}

[CustomEditor(typeof(AstraShipRig))]
public sealed class AstraShipRigEditor:Editor
{
    bool bindings;
    public override void OnInspectorGUI()
    {
        serializedObject.Update();EditorGUILayout.PropertyField(serializedObject.FindProperty("usage"));EditorGUILayout.PropertyField(serializedObject.FindProperty("rotateGravityWithScene"));
        EditorGUILayout.HelpBox("拖入场景后从舱内开始。可移动、旋转，缩放保持1。运行时会以船为原点整体重定位当前场景；不需要手动搬动环境。一个场景仅一艘PlayerShip，其他实例设Scenery。没有船外入口或自由行走。",MessageType.Info);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("onReady"));bindings=EditorGUILayout.Foldout(bindings,"Internal prefab references",true);
        if(bindings)foreach(var field in new[]{"systemsRoot","hull","cockpitCamera"})EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
        serializedObject.ApplyModifiedProperties();
        if(GUILayout.Button("Validate ships in current scene"))AstraShipPrefabTools.ValidateScene();
    }
}




