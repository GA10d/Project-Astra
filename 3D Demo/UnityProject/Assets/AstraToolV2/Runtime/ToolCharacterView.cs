using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace Astra.PerformanceToolV2
{
    public static class ToolCharacterView
    {
        static readonly Dictionary<string,Texture2D> FacePaint=new Dictionary<string,Texture2D>();
        public static Texture2D FaceMark(ToolSpecies species,int id,ToolCharacterLook look=null)
        {
            if(id<=0)return null;
            string path="FaceMarks/"+(ToolCreatorOptions.Alien(species)?"Alien":ToolCreatorOptions.Female(look)?"Female":"Human")+"_"+id.ToString("00");
            if(!FacePaint.TryGetValue(path,out var texture)){texture=Resources.Load<Texture2D>(path);FacePaint[path]=texture;}
            return texture;
        }
        public static GameObject Create(ToolCharacter character,Transform parent,bool motion=true)
        {
            if(!character||!character.catalog)throw new ArgumentException("角色缺少 V2 资源库");
            var model=character.look.creatorEnabled?character.catalog.modularModel:character.sourceModel;
            if(!model)throw new ArgumentException("角色缺少源模型");
            var root=new GameObject(character.displayName);root.transform.SetParent(parent,false);
            var rig=UnityEngine.Object.Instantiate(model,root.transform);rig.name="Quaternius Rig";
            rig.transform.localPosition=Vector3.zero;rig.transform.localRotation=Quaternion.Euler(0,180,0);
            var animator=rig.GetComponent<Animator>();
            if(!animator||!animator.avatar||!animator.avatar.isValid||!animator.avatar.isHuman)
                throw new InvalidOperationException("无效 Humanoid Avatar: "+character.id);
            animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            ApplyLook(root,character);
            if(motion){var driver=root.AddComponent<ToolMotion>();driver.catalog=character.catalog;}
            return root;
        }
        public static void ApplyLook(GameObject root,ToolCharacter character)
        {
            var look=character.look;bool modular=look.creatorEnabled;string species=ToolCreatorOptions.Alien(character.species)?"Alien":"Human";
            if(modular)ToolCreatorOptions.Normalize(look,character.species);
            var facePaint=modular?FaceMark(character.species,look.facialMark,look):null;
            string moduleSpecies=species=="Human"&&ToolCreatorOptions.Female(look)?"Female":species;
            Transform rig=root.transform.Find("Quaternius Rig");
            if(rig)rig.localScale=Vector3.one*(modular?Mathf.Clamp(look.bodyScale,.9f,1.1f):1);
            foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.updateWhenOffscreen=true;
                if(modular)
                {
                    string n=renderer.name;
                    string headName=species=="Alien"&&look.faceShape>0?"Head_Alien_"+look.faceShape.ToString("00"):"Head_"+moduleSpecies;
                    bool visible=n==headName||n=="Body_"+look.outfitStyle.ToString("00")||n=="Legs_"+look.legStyle.ToString("00")||n=="Feet_"+look.footStyle.ToString("00")||
                        n=="Hair_"+moduleSpecies+"_"+look.hairStyle.ToString("00")||n=="FaceAcc_"+moduleSpecies+"_"+look.faceAccessory.ToString("00")||n=="Gear_"+look.gearAccessory.ToString("00");
                    // Renderer enabled state is serialized into exported prefabs; the rig stays active.
                    if(species=="Human"&&look.hairStyle==(ToolCreatorOptions.Female(look)?13:11)&&n.StartsWith("FaceAcc_"))visible=false;
                    renderer.enabled=visible;
                    Mesh mesh=renderer.sharedMesh;
                    for(int i=0;i<mesh.blendShapeCount;i++)
                    {
                        string key=mesh.GetBlendShapeName(i);key=key.Substring(key.LastIndexOf('.')+1);
                        float weight=(species=="Human"&&key=="Face"+look.faceShape.ToString("00"))||(species=="Alien"&&key=="AlienFit"+look.faceShape.ToString("00"))||key=="Eye"+look.eyeShape.ToString("00")||key=="Brow"+look.browShape.ToString("00")||key=="Nose"+look.noseShape.ToString("00")||key=="Mouth"+look.mouthShape.ToString("00")||key=="Body"+look.bodyType.ToString("00")?100:0;
                        if(key=="ChinRound")weight=look.chinStyle==1?100:0;
                        if(key=="ChinSquare")weight=look.chinStyle==2?100:0;
                        if(key=="WidthWide")weight=Mathf.Clamp01((look.faceWidth-1)/.08f)*100;
                        if(key=="WidthNarrow")weight=Mathf.Clamp01((1-look.faceWidth)/.08f)*100;
                        if(key=="JawWide")weight=Mathf.Clamp01(look.jawWidth)*100;
                        if(key=="JawNarrow")weight=Mathf.Clamp01(-look.jawWidth)*100;
                        if(n.StartsWith("Hair_Alien_")&&(key=="WidthWide"||key=="WidthNarrow"))weight*=ToolCreatorOptions.CrownWidth(look.faceShape);
                        renderer.SetBlendShapeWeight(i,weight);
                    }
                }
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    if(!materials[i])continue;
                    string name=materials[i].name.Replace(" (Instance)","");
                    var material=character.catalog.Material(name);if(material)materials[i]=material;
                    if(!modular)continue;
                    Color color=materials[i].color;
                    switch(name)
                    {
                        case "Q_Skin":color=look.skin;break;case "Q_SkinShade":color=look.skin*.78f;color.a=1;break;
                        case "Q_Hair":case "Q_Brow":color=look.hair;break;case "Q_Eye":color=look.eyes;break;
                        case "Q_Suit":color=look.suit;break;case "Q_Trim":case "Q_Accent":color=look.accent;break;
                    }
                    // Crown body color is independently editable; membranes/crystals use accent.
                    if(renderer.name.StartsWith("Hair_Alien_")&&(name=="Q_Skin"||name=="Q_SkinShade"))
                    {color=look.hair*(name=="Q_SkinShade"?.78f:1);color.a=1;}
                    var block=new MaterialPropertyBlock();block.SetColor("_Color",color);
                    bool paint=facePaint&&renderer.name.StartsWith("Head_")&&(name=="Q_Skin"||name=="Q_SkinShade");
                    block.SetFloat("_FaceMarkEnabled",paint?1:0);block.SetColor("_FaceMarkColor",look.accent);
                    if(paint)block.SetTexture("_FaceMarkTex",facePaint);
                    renderer.SetPropertyBlock(block,i);
                }
                renderer.sharedMaterials=materials;
            }
        }
        public static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
    }
}
