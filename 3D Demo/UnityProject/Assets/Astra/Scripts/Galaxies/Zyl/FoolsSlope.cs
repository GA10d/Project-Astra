using System;
using System.Collections.Generic;
using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    // Private sampled route on the authored hill; no NavMesh, editor baking or scene edits.
    public sealed class FoolsSlope : IDisposable
    {
        sealed class Surface {public MeshCollider collider;public Bounds bounds;}
        readonly List<GameObject> proxies=new List<GameObject>();
        readonly List<Surface> ground=new List<Surface>(),obstacles=new List<Surface>(),stones=new List<Surface>();
        readonly List<Vector3> points=new List<Vector3>();
        readonly List<float> lengths=new List<float>();
        readonly Matrix4x4 frame,inverse;
        public float Length {get;private set;}
        public Vector3 Up {get;private set;}
        public Vector3 Forward {get;private set;}
        public Vector3 Start {get{return points[0];}}
        public Vector3 End {get{return points[points.Count-1];}}
        public int PointCount {get{return points.Count;}}
        public FoolsSlope(Transform environment,Matrix4x4 authoredFrame,Vector3 start)
        {
            frame=authoredFrame;inverse=frame.inverse;Up=frame.MultiplyVector(Vector3.up).normalized;Forward=frame.MultiplyVector(Vector3.forward).normalized;
            try
            {
                foreach(var f in environment.GetComponentsInChildren<MeshFilter>(true))
                {
                    bool floor=f.name.StartsWith("Humus");bool obstacle=f.name.StartsWith("Stone")||f.name.StartsWith("Bark")||f.name.StartsWith("Leaf");
                    if((!floor&&!obstacle)||!f.sharedMesh)continue;
                    var renderer=f.GetComponent<Renderer>();if(!renderer||!renderer.enabled)continue;
                    var go=new GameObject("Zyl15 | temporary slope query");go.hideFlags=HideFlags.HideAndDontSave;proxies.Add(go);
                    go.transform.SetPositionAndRotation(f.transform.position,f.transform.rotation);go.transform.localScale=f.transform.lossyScale;
                    var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=f.sharedMesh;
                    var surface=new Surface{collider=collider,bounds=renderer.bounds};(floor?ground:obstacles).Add(surface);if(f.name.StartsWith("Stone"))stones.Add(surface);
                }
                if(ground.Count==0)throw new InvalidOperationException("上坡演出找不到Humus坡面网格。");
                Build(inverse.MultiplyPoint3x4(start));
            }
            catch{Dispose();throw;}
        }
        bool Hit(List<Surface> surfaces,Ray ray,float distance,out RaycastHit nearest)
        {
            nearest=new RaycastHit();float best=distance;bool found=false;bool previous=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
            try{foreach(var s in surfaces){float bound;if(!s.bounds.IntersectRay(ray,out bound)||bound>distance)continue;RaycastHit hit;if(s.collider.Raycast(ray,out hit,best)){nearest=hit;best=hit.distance;found=true;}}}
            finally{Physics.queriesHitBackfaces=previous;}return found;
        }
        public Vector3 Ground(Vector3 world)
        {
            var p=inverse.MultiplyPoint3x4(world);RaycastHit hit;
            if(!Hit(ground,new Ray(frame.MultiplyPoint3x4(new Vector3(p.x,80,p.z)),-Up),150,out hit))throw new InvalidOperationException("上坡路径离开了当前坡面。");var result=hit.point;RaycastHit support;
            // Actual stone top surfaces supplement Humus; steep transitions remain rejected by the route search.
            if(Hit(stones,new Ray(result+Up*60,-Up),60,out support))result=support.point;
            return result;
        }
        bool Blocked(Vector3 foot,float radius=.23f)
        {
            var r=frame.MultiplyVector(Vector3.right).normalized;RaycastHit hit;
            if(Hit(stones,new Ray(foot+Up*60,-Up),60,out hit)&&Vector3.Dot(hit.point-foot,Up)>.13f)return true;
            foreach(var offset in new[]{Vector3.zero,r*radius,-r*radius,Forward*radius,-Forward*radius})
                if(Hit(obstacles,new Ray(foot+offset+Up*1.35f,-Up),1.22f,out hit))return true;
            foreach(float height in new[]{.65f,1.15f})foreach(var axis in new[]{r,Forward})if(Hit(obstacles,new Ray(foot+Up*height-axis*radius,axis),radius*2,out hit))return true;
            return false;
        }
        // Authored-space samples reconstructed from the user's green stroke and yellow camera mark.
        // They transform with the existing stomach alignment; scene anchors are never rewritten.
        public static readonly Vector3[] AuthoredRoute={
            new Vector3(-1.0625f,0,12.6754f),new Vector3(-1.9938f,0,16.6366f),
            new Vector3(-2.5377f,0,22.0958f),new Vector3(-2.7115f,0,27.2755f),
            new Vector3(-2.6933f,0,33.0466f),new Vector3(1f,0,38f)};
        public Vector3 FixedCameraPosition {get;private set;}
        public float MaxRouteAdjustment {get;private set;}
        void Build(Vector3 authoredStart)
        {
            FixedCameraPosition=frame.MultiplyPoint3x4(new Vector3(.3799f,1.55f,3.8840f));
            // Reproject the yellow dot onto the authored lens-height plane; keep the lens itself at the mark.

            var lateral=frame.MultiplyVector(Vector3.right).normalized;
            for(int segment=0;segment<AuthoredRoute.Length-1;segment++)
            {
                var a=AuthoredRoute[segment];var c=AuthoredRoute[segment+1];int steps=Mathf.CeilToInt(Vector3.Distance(a,c)/.24f);
                for(int n=0;n<=steps;n++)
                {
                    if(segment>0&&n==0)continue;
                    var expected=frame.MultiplyPoint3x4(Vector3.Lerp(a,c,n/(float)steps));var p=Ground(expected);
                    bool endpoint=(segment==0&&n==0)||(segment==AuthoredRoute.Length-2&&n==steps);
                    // Only tiny local clearance adjustments are allowed; never find an unrelated detour.
                    if(!endpoint&&Blocked(p))foreach(float offset in new[]{.15f,-.15f,.3f,-.3f,.45f,-.45f})
                    {
                        var candidate=Ground(expected+lateral*offset);if(Blocked(candidate))continue;
                        p=candidate;MaxRouteAdjustment=Mathf.Max(MaxRouteAdjustment,Mathf.Abs(offset));break;
                    }
                    points.Add(p);
                }
            }
            lengths.Add(0);for(int i=1;i<points.Count;i++){Length+=Vector3.Distance(points[i-1],points[i]);lengths.Add(Length);}
        }
        public Vector3 Position(float distance)
        {
            if(distance<=0)return points[0];if(distance>=Length)return End;
            for(int i=1;i<points.Count;i++)if(distance<=lengths[i])return Vector3.Lerp(points[i-1],points[i],Mathf.InverseLerp(lengths[i-1],lengths[i],distance));return End;
        }
        public Vector3 Direction(float distance)
        {
            var delta=Position(Mathf.Min(Length,distance+.5f))-Position(Mathf.Max(0,distance-.5f));delta=Vector3.ProjectOnPlane(delta,Up).normalized;
            return Vector3.Slerp(delta,Forward,Mathf.SmoothStep(0,1,Mathf.InverseLerp(Length-2,Length,distance))).normalized;
        }
        public Vector3 CameraPosition(Vector3 desired,Vector3 target)
        {
            var floor=Ground(desired);if(Vector3.Dot(desired-floor,Up)<.6f)desired=floor+Up*.6f;
            foreach(var p in new[]{desired,desired+Up*.8f,Vector3.Lerp(desired,target,.4f)+Up*.5f})
            {var delta=target-p;RaycastHit hit;if(!Hit(obstacles,new Ray(p,delta.normalized),delta.magnitude-.1f,out hit)&&!Hit(ground,new Ray(p,delta.normalized),delta.magnitude-.1f,out hit))return p;}
            return Vector3.Lerp(desired,target,.75f)+Up*.25f;
        }
        public void Dispose(){foreach(var p in proxies)if(p){p.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(p);else UnityEngine.Object.DestroyImmediate(p);}proxies.Clear();ground.Clear();obstacles.Clear();stones.Clear();}
    }
}
