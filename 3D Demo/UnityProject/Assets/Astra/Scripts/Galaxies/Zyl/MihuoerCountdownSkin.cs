using System.Collections.Generic;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    /// <summary>Per-instance opaque material: no through-wall UI, no shared material/texture mutation.</summary>
    public sealed class MihuoerCountdownSkin : MonoBehaviour
    {
        readonly List<Material> materials=new List<Material>();
        readonly Dictionary<Renderer,Material[]> original=new Dictionary<Renderer,Material[]>();
        readonly Dictionary<SkinnedMeshRenderer,Mesh> originalMeshes=new Dictionary<SkinnedMeshRenderer,Mesh>();
        MihuoerCountdown service;
        ZylPresentationParameters parameters;
        bool preview;
        double previewSeconds=300;
        public static MihuoerCountdownSkin Attach(GameObject model,MihuoerCountdown timer,ZylPresentationParameters settings)
        {var skin=model.AddComponent<MihuoerCountdownSkin>();skin.Initialize(timer,settings);return skin;}
        void Initialize(MihuoerCountdown timer,ZylPresentationParameters settings)
        {
            service=timer;parameters=settings;
            var shader=Resources.Load<Shader>("Galaxies/Zyl/Countdown/SubdermalCountdown");var mask=Resources.Load<Texture2D>("Galaxies/Zyl/Countdown/CountdownWoundMask");
            var mesh=Resources.Load<Mesh>("Galaxies/Zyl/Countdown/MihuoerCountdownSkinMesh");
            if(!shader||!mask||!mesh){Debug.LogError("米霍尔倒计时资源加载失败：Shader="+(shader?"正常":"缺失")+"，二维破口遮罩="+(mask?"正常":"缺失或类型错误")+"，坐标网格="+(mesh?"正常":"缺失")+"。请使用 Astra > Zyl > 校验米霍尔倒计时资源。");return;}
            foreach(var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!renderer.sharedMesh||renderer.sharedMesh.vertexCount!=mesh.vertexCount){Debug.LogError("米霍尔倒计时网格与角色不匹配。");continue;}
                originalMeshes[renderer]=renderer.sharedMesh;renderer.sharedMesh=mesh;
                original[renderer]=renderer.sharedMaterials;var replacements=(Material[])renderer.sharedMaterials.Clone();
                for(int i=0;i<replacements.Length;i++)if(replacements[i]){var material=new Material(replacements[i]);material.shader=shader;material.SetTexture("_CountdownWoundMask",mask);material.SetFloat("_CountdownWidth",settings.countdownWidth);material.SetFloat("_CountdownHeight",settings.countdownHeight);material.SetFloat("_CountdownHalo",settings.countdownHalo);material.SetVector("_CountdownOffset",new Vector4(settings.countdownOffsetX,settings.countdownOffsetY,0,0));materials.Add(material);replacements[i]=material;}renderer.sharedMaterials=replacements;
            }
            UpdateDisplay();
        }
        public void SetPreview(double seconds){preview=true;previewSeconds=seconds;UpdateDisplay();}
        void LateUpdate(){UpdateDisplay();}
        public void UpdateDisplay()
        {
            if(service&&service.Parameters!=null)parameters=service.Parameters;
            var clock=service?service.Clock:null;double seconds=preview?previewSeconds:clock!=null?clock.DisplaySeconds:parameters.countdownInitialSeconds;
            bool visible=parameters.countdownEnabled>.5f&&(preview||clock!=null&&clock.State.visible);
            int total=(int)System.Math.Ceiling(System.Math.Max(0,System.Math.Min(5999,seconds))),minutes=total/60,remainder=total%60;
            Color color;ColorUtility.TryParseHtmlString(seconds<=parameters.countdownWarningSeconds?parameters.countdownWarningColor:parameters.countdownColor,out color);
            foreach(var material in materials){material.SetFloat("_CountdownWidth",parameters.countdownWidth);material.SetFloat("_CountdownHeight",parameters.countdownHeight);material.SetFloat("_CountdownHalo",parameters.countdownHalo);material.SetVector("_CountdownOffset",new Vector4(parameters.countdownOffsetX,parameters.countdownOffsetY,0,0));material.SetFloat("_CountdownVisible",visible?1:0);material.SetVector("_CountdownDigits",new Vector4(minutes/10,minutes%10,remainder/10,remainder%10));material.SetColor("_CountdownColor",color);material.SetFloat("_CountdownBrightness",parameters.countdownBrightness);}
        }
        void OnDestroy(){foreach(var pair in original)if(pair.Key)pair.Key.sharedMaterials=pair.Value;foreach(var pair in originalMeshes)if(pair.Key)pair.Key.sharedMesh=pair.Value;foreach(var material in materials)if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
    }
}

