using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Astra.PerformanceToolV2
{
    public static class ToolLogic
    {
        public static bool Test(ToolCondition condition, IDictionary<string,int> state)
        {
            int actual=state.TryGetValue(condition.key,out var value)?value:0;
            switch(condition.compare)
            {
                case ToolCompare.Equal:return actual==condition.value;
                case ToolCompare.NotEqual:return actual!=condition.value;
                case ToolCompare.Greater:return actual>condition.value;
                case ToolCompare.GreaterOrEqual:return actual>=condition.value;
                case ToolCompare.Less:return actual<condition.value;
                default:return actual<=condition.value;
            }
        }
        public static void Apply(ToolEffect[] effects, IDictionary<string,int> state)
        {
            if(effects==null)return;
            foreach(var effect in effects)state[effect.key]=(state.TryGetValue(effect.key,out var current)?current:0)+effect.delta;
        }
        public static string Exit(ToolGraphNode node,string slot)
        {
            foreach(var edge in node.edges)if(edge.slot==slot)return edge.target;
            return null;
        }
        public static ToolChoice Choice(ToolChoice[] options,string id)
        {
            if(options==null)return null;
            foreach(var option in options)if(option.id==id)return option;
            return null;
        }
    }



    public static class ToolBlocking
    {
        public static void Apply(ToolCameraSpan shot,Transform subject,Transform listener)
        {
            if(subject)subject.rotation=Quaternion.identity;if(listener)listener.rotation=Quaternion.identity;
            if(!subject||!listener||shot==null||(shot.shot!=ToolShot.TwoShot&&shot.shot!=ToolShot.OverShoulder))return;
            Vector3 direction=listener.position-subject.position;direction.y=0;if(direction.sqrMagnitude<.01f)return;
            // Imported rigs face -Z; the outer actor transform owns staging, never the Humanoid bones.
            subject.rotation=Quaternion.LookRotation(-direction);listener.rotation=Quaternion.LookRotation(direction);
        }
    }
    public static class ToolCameraMath
    {
        public static void Apply(Camera camera,ToolCameraSpan span,Transform actor,Transform partner,bool crawler,bool tall,float time)
        {
            Vector3 p=actor?actor.position:Vector3.zero;
            var animator=actor?actor.GetComponentInChildren<Animator>():null;
            var head=animator&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head):null;
            float h=head?head.position.y-p.y+.08f:crawler?.76f:1.6f;
            Vector3 target=p+Vector3.up*(crawler?.62f:h-.18f),pos;
            float fov=36;
            switch(span==null?ToolShot.Establishing:span.shot)
            {
                case ToolShot.Establishing:pos=new Vector3(.35f,2.0f,-5.2f);target=new Vector3(0,1.06f,0);fov=38;break;
                case ToolShot.Medium:pos=p+new Vector3(.20f,h+.14f,crawler?-2.7f:-2.55f);fov=32;break;
                case ToolShot.Close:pos=p+new Vector3(.05f,h+.07f,tall?-1.65f:-1.4f);target=p+Vector3.up*(h+(tall?.1f:0));fov=tall?38:34;break;
                case ToolShot.TwoShot:
                    if(partner){target=(p+partner.position)*.5f+Vector3.up*(h-.18f);fov=40;float distance=Mathf.Max(2.3f,(Mathf.Abs(p.x-partner.position.x)+1.05f)/(2*Mathf.Tan(fov*Mathf.Deg2Rad*.5f)*camera.aspect));pos=target+new Vector3(0,.08f,-distance);}
                    else{target=p+Vector3.up*(crawler?.72f:1.3f);pos=p+new Vector3(.2f,h+.1f,-3.3f);fov=40;}
                    break;
                case ToolShot.OverShoulder:
                    target=p+Vector3.up*(crawler?.7f:h-.10f);
                    if(partner){Vector3 away=(partner.position-p).normalized;Vector3 side=Vector3.Cross(Vector3.up,away);pos=partner.position+away*.62f+side*.42f+Vector3.up*(h+.04f);}
                    else pos=p+new Vector3(1.0f,h+.05f,-2.55f);
                    fov=partner?38:38;break;
                case ToolShot.Profile:
                    pos=p+(crawler?new Vector3(-2.2f,1.25f,-3.4f):new Vector3(-1.35f,h+.04f,-2.1f));
                    target=p+Vector3.up*(crawler?.7f:h-.18f);fov=crawler?42:34;break;
                case ToolShot.LowAngle:pos=p+new Vector3(.24f,crawler?.50f:.88f,-2.15f);fov=34;break;
                case ToolShot.Insert:pos=new Vector3(2.55f,1.45f,-1.6f);target=new Vector3(2.8f,1.13f,1.1f);fov=38;break;
                case ToolShot.Tracking:pos=p+new Vector3(.37f,crawler?1.05f:1.48f,-3.6f);target=p+Vector3.up*(crawler?.72f:1.03f);fov=38;pos.x+=Mathf.Sin(time*.24f)*.12f;break;
                default:
                    pos=p+span.customPosition;target=pos+Quaternion.Euler(span.customEuler)*Vector3.forward;
                    fov=span.customFov;break;
            }
            camera.transform.position=pos;camera.transform.rotation=Quaternion.LookRotation(target-pos);camera.fieldOfView=fov;
        }
    }




}
