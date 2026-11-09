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
        readonly List<Mesh> generatedMeshes=new List<Mesh>();
        MihuoerCountdown service;
        ZylPresentationParameters parameters;
        bool preview;
        double previewSeconds=300;
        public static MihuoerCountdownSkin Attach(GameObject model,MihuoerCountdown timer,ZylPresentationParameters settings,bool useModelSkinning=false)
        {var skin=model.AddComponent<MihuoerCountdownSkin>();skin.Initialize(timer,settings,useModelSkinning);return skin;}
        void Initialize(MihuoerCountdown timer,ZylPresentationParameters settings,bool useModelSkinning)
        {
            service=timer;parameters=settings;
            var shader=Resources.Load<Shader>("Galaxies/Zyl/Countdown/SubdermalCountdown");var mask=Resources.Load<Texture2D>("Galaxies/Zyl/Countdown/CountdownWoundMask");
            var mesh=Resources.Load<Mesh>("Galaxies/Zyl/Countdown/MihuoerCountdownSkinMesh");
            if(!shader||!mask||(!useModelSkinning&&!mesh)){Debug.LogError("米霍尔倒计时资源加载失败：Shader="+(shader?"正常":"缺失")+"，二维破口遮罩="+(mask?"正常":"缺失或类型错误")+"，坐标网格="+(mesh?"正常":"缺失")+"。请使用 Astra > Zyl > 校验米霍尔倒计时资源。");return;}
            foreach(var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!renderer.sharedMesh){Debug.LogError("米霍尔角色缺少蒙皮网格。");continue;}
                var source=renderer.sharedMesh;
                if(useModelSkinning)
                {
                    // Rest-space coordinates only: retain this model's bone indices, weights and bind poses.
                    var derived=Instantiate(source);derived.name=source.name+" | countdown coordinates";
                    var vertices=source.vertices;var coords=new List<Vector4>(vertices.Length);
                    foreach(var vertex in vertices)coords.Add((Vector4)transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex)));
                    derived.SetUVs(2,coords);generatedMeshes.Add(derived);originalMeshes[renderer]=source;renderer.sharedMesh=derived;
                }
                else
                {
                    if(source.vertexCount!=mesh.vertexCount){Debug.LogError("米霍尔倒计时网格与角色不匹配。");continue;}
                    originalMeshes[renderer]=source;renderer.sharedMesh=mesh;
                }
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
            int total=(int)System.Math.Ceiling(System.Math.Max(0,System.Math.Min(MihuoerCountdownClock.MaxSeconds,seconds)));
            int hours=total/3600, minutes=total/60%60, remainder=total%60;
            Color color;ColorUtility.TryParseHtmlString(seconds<=parameters.countdownWarningSeconds?parameters.countdownWarningColor:parameters.countdownColor,out color);
            foreach(var material in materials){material.SetFloat("_CountdownWidth",parameters.countdownWidth);material.SetFloat("_CountdownHeight",parameters.countdownHeight);material.SetFloat("_CountdownHalo",parameters.countdownHalo);material.SetVector("_CountdownOffset",new Vector4(parameters.countdownOffsetX,parameters.countdownOffsetY,0,0));material.SetFloat("_CountdownVisible",visible?1:0);material.SetVector("_CountdownHours",new Vector4(hours/10,hours%10,0,0));material.SetVector("_CountdownDigits",new Vector4(minutes/10,minutes%10,remainder/10,remainder%10));material.SetColor("_CountdownColor",color);material.SetFloat("_CountdownBrightness",parameters.countdownBrightness);}
        }
        internal static Vector3 FindCountdownSurface(Transform actor, Transform chest, ZylPresentationParameters settings)
        {
            // Use the countdown shader's rest-space coordinates to locate the skin over the source.
            Vector2 displayCenter = new Vector2(-settings.countdownOffsetX, .795f + settings.countdownOffsetY);
            SkinnedMeshRenderer bestSkin = null;
            int bestIndex = 0;
            float bestDistance = float.PositiveInfinity;
            var rest = new List<Vector4>();
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!skin.sharedMesh) continue;
                rest.Clear();
                skin.sharedMesh.GetUVs(2, rest);
                for (int i = 0; i < rest.Count; i++)
                {
                    Vector4 point = rest[i];
                    if (point.z <= .015f || point.y < .69f || point.y > .90f || Mathf.Abs(point.x) > .17f)
                        continue;
                    float distance = (new Vector2(point.x, point.y) - displayCenter).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestSkin = skin;
                    bestIndex = i;
                    bestDistance = distance;
                }
            }
            if (!bestSkin) return chest.position;

            var mesh = bestSkin.sharedMesh;
            var weights = mesh.boneWeights;
            if (bestIndex >= weights.Length) return chest.position;
            var vertex = mesh.vertices[bestIndex];
            var bind = mesh.bindposes;
            var bones = bestSkin.bones;
            BoneWeight weight = weights[bestIndex];

            // Bone matrices already include the FBX hierarchy's scale. The sum is in world space.
            Vector3 WeightedPoint(int index, float amount)
            {
                if (amount == 0) return Vector3.zero;
                return bones[index].TransformPoint(bind[index].MultiplyPoint3x4(vertex)) * amount;
            }

            return WeightedPoint(weight.boneIndex0, weight.weight0)
                + WeightedPoint(weight.boneIndex1, weight.weight1)
                + WeightedPoint(weight.boneIndex2, weight.weight2)
                + WeightedPoint(weight.boneIndex3, weight.weight3);
        }

        void OnDestroy(){foreach(var pair in original)if(pair.Key)pair.Key.sharedMaterials=pair.Value;foreach(var pair in originalMeshes)if(pair.Key)pair.Key.sharedMesh=pair.Value;foreach(var mesh in generatedMeshes)if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}foreach(var material in materials)if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
    }
}

