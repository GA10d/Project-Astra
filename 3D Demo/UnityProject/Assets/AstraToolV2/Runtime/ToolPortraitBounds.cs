using System.Collections.Generic;
using UnityEngine;

namespace Astra.PerformanceToolV2
{
    // Renderer.bounds may still contain the preceding GPU skinning frame immediately
    // after a manual Playables seek. Use the sampled mesh for head/hairstyle framing.
    public sealed class ToolPortraitBounds:MonoBehaviour
    {
        SkinnedMeshRenderer[] renderers;
        Mesh baked;
        readonly List<Vector3> vertices=new List<Vector3>(4096);
        public bool TryGetBounds(out Bounds bounds)
        {
            if(renderers==null)renderers=GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if(!baked)baked=new Mesh{name="Portrait bounds scratch",hideFlags=HideFlags.HideAndDontSave};
            bounds=new Bounds();bool found=false;
            foreach(var renderer in renderers)
            {
                if(!renderer||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                string n=renderer.name;
                if(!(n.StartsWith("Head_")||n.StartsWith("Hair_")||n.StartsWith("FaceAcc_")))continue;
                // FBX uses a centimetre conversion on the skinned mesh transform.
                // Match Unity's rendered scale, as in the skinning validation path.
                renderer.BakeMesh(baked,true);baked.GetVertices(vertices);
                var matrix=renderer.transform.localToWorldMatrix;
                foreach(var vertex in vertices)
                {
                    var point=matrix.MultiplyPoint3x4(vertex);
                    if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
                }
            }
            return found;
        }
        void OnDestroy(){if(baked){if(Application.isPlaying)Destroy(baked);else DestroyImmediate(baked);}}
    }
}
