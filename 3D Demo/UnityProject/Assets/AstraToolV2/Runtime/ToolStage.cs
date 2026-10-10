using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Astra.PerformanceToolV2
{
    public sealed class ToolStage : MonoBehaviour
    {
        Material wallMaterial,floorMaterial,metalMaterial;
        Light key;
        Transform seat;
        readonly List<Material> owned=new List<Material>();
        public bool IsBuilt => wallMaterial && floorMaterial && key;
        public void Build(bool orbitPreview=false)
        {
            foreach(var m in owned)DisposeObject(m);owned.Clear();
            foreach(Transform child in transform)DisposeObject(child.gameObject);
            wallMaterial=Mat(new Color(.29f,.32f,.28f));floorMaterial=Mat(new Color(.14f,.17f,.14f));metalMaterial=Mat(new Color(.12f,.14f,.12f));
            if(orbitPreview)
            {
                // Creator cameras orbit through a larger volume than authored story shots.
                // Keep the inspection room outside the entire full-body camera orbit.
                Box("floor",new Vector3(0,-.075f,0),new Vector3(14,.15f,14),floorMaterial);
                Box("back wall",new Vector3(0,3.5f,6),new Vector3(14,7,.12f),wallMaterial);
                Box("front wall",new Vector3(0,3.5f,-6),new Vector3(14,7,.12f),wallMaterial);
                Box("left side",new Vector3(-6,3.5f,0),new Vector3(.12f,7,14),wallMaterial);
                Box("right side",new Vector3(6,3.5f,0),new Vector3(.12f,7,14),wallMaterial);
                for(int i=-8;i<=8;i++)
                foreach(float z in new[]{-5.925f,5.925f})
                    Box("panel seam",new Vector3(i*.62f,3.5f,z),new Vector3(.018f,7,.02f),metalMaterial);
                var inspectionFill=new GameObject("inspection rear fill");inspectionFill.transform.SetParent(transform,false);
                inspectionFill.transform.localPosition=new Vector3(1.2f,3.1f,3f);
                var inspectionLight=inspectionFill.AddComponent<Light>();inspectionLight.type=LightType.Point;
                inspectionLight.range=8;inspectionLight.intensity=1.5f;inspectionLight.color=new Color(.80f,.86f,.82f);
            }
            else
            {
            Box("floor",new Vector3(0,-.075f,.1f),new Vector3(9,.15f,7),floorMaterial);
            Box("back wall",new Vector3(0,1.65f,1.5f),new Vector3(9,3.3f,.12f),wallMaterial);
            Box("left side",new Vector3(-4.4f,1.65f,-.2f),new Vector3(.1f,3.3f,3.6f),wallMaterial);
            Box("right side",new Vector3(4.4f,1.65f,-.2f),new Vector3(.1f,3.3f,3.6f),wallMaterial);
            Box("ceiling",new Vector3(0,3.35f,-.2f),new Vector3(9,.1f,7),wallMaterial);
            for(int i=-6;i<=6;i++)Box("panel seam",new Vector3(i*.62f,1.6f,1.425f),new Vector3(.018f,3.2f,.02f),metalMaterial);
            Box("relay cabinet",new Vector3(2.8f,.8f,1.12f),new Vector3(.65f,1.45f,.5f),metalMaterial);
            Box("status light",new Vector3(2.8f,1.27f,.86f),new Vector3(.28f,.05f,.025f),Mat(new Color(.48f,.72f,.29f),true));
            }
            var light=new GameObject("stage key light");light.transform.SetParent(transform,false);light.transform.localPosition=new Vector3(-1.2f,2.9f,-2.2f);
            light.transform.LookAt(transform.TransformPoint(new Vector3(0,1.25f,0)));
            key=light.AddComponent<Light>();key.type=LightType.Spot;key.range=9;key.spotAngle=105;key.intensity=2.25f;
            var fill=new GameObject("stage fill light");fill.transform.SetParent(transform,false);fill.transform.localPosition=new Vector3(2.2f,1.6f,-2.0f);
            var f=fill.AddComponent<Light>();f.type=LightType.Point;f.range=7;f.intensity=.85f;f.color=new Color(.70f,.82f,.76f);
            var bounce=new GameObject("stage front bounce");bounce.transform.SetParent(transform,false);
            bounce.transform.localRotation=Quaternion.Euler(30,-25,0);
            var b=bounce.AddComponent<Light>();b.type=LightType.Directional;b.intensity=.22f;b.color=new Color(.80f,.86f,.82f);
            var rim=new GameObject("stage side fill");rim.transform.SetParent(transform,false);rim.transform.localRotation=Quaternion.Euler(25,-135,0);
            var sideLight=rim.AddComponent<Light>();sideLight.type=LightType.Directional;sideLight.intensity=.65f;sideLight.color=new Color(.77f,.83f,.81f);
            seat=new GameObject("Seated pose support").transform;seat.SetParent(transform,false);
            int before=transform.childCount;
            Box("Seat cushion",new Vector3(0,.46f,0),new Vector3(.46f,.09f,.42f),metalMaterial);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Box("Seat leg",new Vector3(x*.18f,.23f,z*.16f),new Vector3(.035f,.45f,.035f),metalMaterial);
            var pieces=new List<Transform>();for(int i=before;i<transform.childCount;i++)pieces.Add(transform.GetChild(i));
            foreach(var piece in pieces)piece.SetParent(seat,false);seat.gameObject.SetActive(false);
        }
        public void SetPose(string action,Vector3 actorPosition,float scale)
        {if(!seat)return;seat.gameObject.SetActive(action.StartsWith("Sitting"));seat.position=actorPosition+Vector3.forward*.11f*scale;seat.localScale=Vector3.one*scale;}
        public void Set(ToolScenePreset scene,ToolFx fx,float time)
        {
            if(scene==null)return;
            wallMaterial.color=scene.wall;floorMaterial.color=scene.floor;
            key.color=fx==ToolFx.EmergencyLight?Color.Lerp(scene.light,new Color(1,.10f,.05f),.65f+.3f*Mathf.Sin(time*9)):scene.light;
        }
        void Box(string name,Vector3 p,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(transform,false);
            go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
            DisposeObject(go.GetComponent<Collider>());
        }
        Material Mat(Color color,bool emissive=false)
        {
            var shader=Shader.Find("Standard");
            if(!shader)throw new InvalidOperationException("Standard shader is missing from player build");
            var m=new Material(shader);m.color=color;
            if(emissive){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*1.5f);}
            owned.Add(m);return m;
        }
        void OnDestroy(){foreach(var m in owned)DisposeObject(m);owned.Clear();}
        static void DisposeObject(UnityEngine.Object o) {if(!o)return;if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
    }
}
