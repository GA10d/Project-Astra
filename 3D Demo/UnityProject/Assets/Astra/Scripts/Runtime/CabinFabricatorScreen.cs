using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace AstraCabin
{
    // The screen rolls down from a fixed housing in front of the printer opening.
    public sealed class CabinFabricatorScreen : MonoBehaviour
    {
        CabinFabricator owner; Canvas canvas; GameObject assembly, eventSystem;
        internal const float MountDepth = -.33f;
        Transform screenBack, lowerRail;
        RectTransform viewport;
        Material caseMaterial; Font font; Text status, progress; Text[] counts=new Text[3];
        Button[] choices=new Button[3]; Button begin, claim, close;
        FabricatorContourGraphic drawing;
        public Canvas WorldCanvas { get { return canvas; } }
        public void Initialize(CabinFabricator printer, Camera camera, Transform mount)
        {
            owner=printer;
            transform.SetParent(mount,true);
            assembly=new GameObject("Retractable terminal | solid screen");assembly.transform.SetParent(transform,false);
            caseMaterial=new Material(Shader.Find("Standard"));caseMaterial.color=new Color(.09f,.12f,.11f);caseMaterial.SetFloat("_Metallic",.7f);caseMaterial.SetFloat("_Glossiness",.35f);
            screenBack=Solid("Screen back",Vector3.zero,new Vector3(.95f,.625f,.035f),assembly.transform);
            Solid("Upper roller housing",new Vector3(0,.32f,-.012f),new Vector3(.99f,.055f,.075f),transform);
            lowerRail=Solid("Lower weighted rail",new Vector3(0,-.32f,-.012f),new Vector3(.99f,.025f,.05f),assembly.transform);
            var go=new GameObject("Interactive display surface",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            go.transform.SetParent(assembly.transform,false);go.transform.localPosition=new Vector3(0,0,-.020f);go.transform.localScale=Vector3.one*.0009f;
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
            ((RectTransform)go.transform).sizeDelta=new Vector2(1000,650);
            viewport=Rect("Screen rollout mask",go.transform,new Rect(0,0,1000,650));
            viewport.gameObject.AddComponent<RectMask2D>();
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},18);
            Image background=Rect("Screen phosphor",viewport,new Rect(0,0,1000,650)).gameObject.AddComponent<Image>();background.color=new Color(.025f,.045f,.045f,1);
            TextAt("FABRICATOR / MK.04   轮廓制造终端",new Rect(24,18,810,40),24);
            close=ButtonAt("返回 [Esc]",new Rect(865,20,110,40),()=>owner.Close());
            for(int i=0;i<3;i++)
            {
                int index=i;choices[i]=ButtonAt(owner.Blueprints[i],new Rect(22,90+i*60,235,48),()=>owner.SelectBlueprint(index));
                counts[i]=TextAt("",new Rect(22,305+i*56,235,50),17);
            }
            claim=ButtonAt("领取已完成部件",new Rect(22,495,235,46),()=>owner.ClaimParts());
            var area=Rect("Blueprint viewport",viewport,new Rect(280,80,695,445));
            area.gameObject.AddComponent<RectMask2D>();
            var contourObject=Rect("Contour and fixed pen",area,new Rect(0,0,695,445)).gameObject;
            // Create the native renderer before Graphic.OnEnable can access it.
            contourObject.AddComponent<CanvasRenderer>();
            drawing=contourObject.AddComponent<FabricatorContourGraphic>();drawing.raycastTarget=false;
            status=TextAt("",new Rect(280,535,680,52),17);
            begin=ButtonAt("开始绘制图纸",new Rect(280,593,320,40),()=>owner.BeginDrawing());
            progress=TextAt("",new Rect(620,593,350,40),17);
            if(!FindObjectOfType<EventSystem>())
            {
                eventSystem=new GameObject("Fabricator pointer events",typeof(EventSystem),typeof(StandaloneInputModule));
                eventSystem.GetComponent<EventSystem>().sendNavigationEvents=false;
            }
            assembly.SetActive(false);
        }
        Transform Solid(string name,Vector3 position,Vector3 scale,Transform parent)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=caseMaterial;
            // Canvas pointer raycaster handles the face. No new cabin navigation hit target.
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            return go.transform;
        }
        RectTransform Rect(string name,Transform parent,Rect rect)
        {
            var go=new GameObject(name,typeof(RectTransform));var rt=(RectTransform)go.transform;rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(rect.x,-rect.y);rt.sizeDelta=rect.size;return rt;
        }
        Text TextAt(string text,Rect rect,int size)
        {
            var rt=Rect(text,viewport,rect);var t=rt.gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.text=text;t.color=new Color(.72f,.88f,.82f);t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;
        }
        Button ButtonAt(string text,Rect rect,UnityEngine.Events.UnityAction action)
        {
            var rt=Rect(text,viewport,rect);var image=rt.gameObject.AddComponent<Image>();image.color=new Color(.14f,.22f,.21f);
            var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=image;b.onClick.AddListener(action);b.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            var t=Rect("Caption",rt,new Rect(8,0,rect.width-16,rect.height)).gameObject.AddComponent<Text>();t.font=font;t.fontSize=17;t.text=text;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(.8f,.94f,.88f);t.raycastTarget=false;return b;
        }
        public void Refresh(Vector3 origin,Quaternion facing,float blend,bool visible,bool interactable)
        {
            if(!canvas)return;
            transform.SetPositionAndRotation(origin+facing*new Vector3(.08f,.15f,MountDepth),facing);
            assembly.SetActive(visible&&blend>0);
            float deployed=Mathf.SmoothStep(0,1,blend);
            float height=.625f*deployed;
            screenBack.localPosition=new Vector3(0,(.625f-height)*.5f,0);
            screenBack.localScale=new Vector3(.95f,height,.035f);
            lowerRail.localPosition=new Vector3(0,Mathf.Lerp(.32f,-.32f,deployed),-.012f);
            viewport.sizeDelta=new Vector2(1000,650*deployed);
            canvas.GetComponent<GraphicRaycaster>().enabled=interactable;
            close.interactable=interactable;
            for(int i=0;i<3;i++)
            {
                choices[i].interactable=interactable&&!owner.Printing&&!owner.Drawing;
                choices[i].GetComponent<Image>().color=owner.Selected==i?new Color(.2f,.4f,.34f):new Color(.14f,.22f,.21f);
                counts[i].text=owner.Blueprints[i]+"   已完成 "+owner.Completed[i]+" / 累计领取 "+owner.Inventory[i];
            }
            begin.interactable=interactable&&!owner.Printing;claim.interactable=interactable;
            begin.GetComponentInChildren<Text>().text=owner.Drawing?"重新开始":owner.Failed?"绘制失败 / 重试":"开始绘制图纸";
            status.text=owner.Message;
            progress.text=owner.Printing?"打印进度 "+Mathf.RoundToInt(owner.PrintProgress*100)+"%":owner.Drawing?"闭合进度 "+Mathf.RoundToInt(owner.TraceProgress/240*100)+"%   Q ↶ / E ↷":"Q / E 旋转 · Esc 返回";
            drawing.Set(owner.Points,owner.Drawing,owner.TraceAngle,owner.TraceOffset,owner.TraceProgress);
        }
        void OnDestroy(){if(font)Destroy(font);if(caseMaterial)Destroy(caseMaterial);if(eventSystem)Destroy(eventSystem);}
    }
}
