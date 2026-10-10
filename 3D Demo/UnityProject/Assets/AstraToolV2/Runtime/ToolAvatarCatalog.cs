using UnityEngine;
using System;
using System.Linq;
namespace Astra.PerformanceToolV2
{
    [CreateAssetMenu(menuName="Astra/Performance Tool V2/Avatar catalog")]
    public sealed class ToolAvatarCatalog:ScriptableObject
    {
        public GameObject modularModel;
        public AnimationClip[] clips=Array.Empty<AnimationClip>();
        public Material[] materials=Array.Empty<Material>();
        public AnimationClip Clip(string id)=>clips.FirstOrDefault(c=>c&&c.name==id);
        public Material Material(string id)=>materials.FirstOrDefault(m=>m&&m.name==id);
    }
}
