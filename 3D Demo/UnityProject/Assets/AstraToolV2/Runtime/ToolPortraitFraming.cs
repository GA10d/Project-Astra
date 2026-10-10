using System;
using UnityEngine;
namespace Astra.PerformanceToolV2
{
    public static class ToolPortraitFraming
    {
        // Fit authored head/headwear bounds so tall crests and hats survive every view angle.
        public static void Apply(Camera camera,GameObject actor,float yaw)
        {
            var cache=actor.GetComponent<ToolPortraitBounds>();if(!cache)cache=actor.AddComponent<ToolPortraitBounds>();
            if(!cache.TryGetBounds(out Bounds bounds))bounds=new Bounds(actor.transform.position+Vector3.up*1.65f,new Vector3(.3f,.45f,.3f));
            camera.fieldOfView=32;float tangent=Mathf.Tan(16*Mathf.Deg2Rad);
            float angle=yaw*Mathf.Deg2Rad;Vector3 target=bounds.center;
            var outward=new Vector3(Mathf.Sin(angle),0,-Mathf.Cos(angle));
            var right=Vector3.Cross(Vector3.up,-outward);float distance=.88f;
            // Account for depth as well as projected height/width at arbitrary yaw.
            // A large fin's near corner can otherwise leave the frame during rotation.
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                var p=Vector3.Scale(bounds.extents,new Vector3(x,y,z));
                float halfHeight=Mathf.Abs(p.y)+.035f;
                float halfWidth=Mathf.Abs(Vector3.Dot(p,right))+.035f;
                distance=Mathf.Max(distance,Vector3.Dot(p,outward)+Mathf.Max(halfHeight/tangent,halfWidth/(tangent*Mathf.Max(.1f,camera.aspect))));
            }
            camera.transform.position=target+outward*distance;camera.transform.LookAt(target);
        }
    }
}
