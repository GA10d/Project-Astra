using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Astra.PerformanceToolV2
{
    [RequireComponent(typeof(Camera))]
    public sealed class ToolScreenFx : MonoBehaviour
    {
        public ToolFx effect;
        Material material;
        void OnRenderImage(RenderTexture src,RenderTexture dst)
        {
            if(effect==ToolFx.None){Graphics.Blit(src,dst);return;}
            if(!material){var shader=Shader.Find("Hidden/Astra/ToolV2/SignalFX");if(shader)material=new Material(shader);}
            if(!material){Graphics.Blit(src,dst);return;}
            material.SetFloat("_Mode",(int)effect);material.SetFloat("_Clock",Time.time);
            Graphics.Blit(src,dst,material);
        }
        void OnDestroy(){if(material)Destroy(material);}
    }
}
