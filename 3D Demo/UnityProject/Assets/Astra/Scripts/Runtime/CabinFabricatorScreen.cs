using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace AstraCabin
{
    // A rigid control board that slides vertically into the printer opening.
    public sealed class CabinFabricatorScreen : MonoBehaviour
    {
        const float CanvasScale=.00094f;
        const int CanvasWidth=1100, CanvasHeight=640;
        internal const float BoardWidth=1.08f, BoardHeight=.63f;
        const float BoardThickness=.024f, Travel=.68f;
        CabinFabricator owner; Canvas canvas; GameObject board, eventSystem;
        Material boardMaterial;
        Vector3 deployedPosition, retractedPosition;
        GraphicRaycaster raycaster;
        RectTransform aperture, viewport;
        Font font; Text status, progress; Text[] counts=new Text[CabinFabricator.BlueprintIds.Length];
        Button[] choices=new Button[CabinFabricator.BlueprintIds.Length]; Button begin, claim, close;
        FabricatorContourGraphic drawing;
        public Canvas WorldCanvas { get { return canvas; } }
        internal Vector3 DeployedCenter => transform.parent.TransformPoint(deployedPosition);
        public void Initialize(CabinFabricator printer, Camera camera, Transform mount)
        {
            owner=printer;
            transform.SetParent(mount,true);
            deployedPosition=transform.localPosition;
            retractedPosition=deployedPosition+mount.InverseTransformVector(Vector3.up*Travel);
            transform.localPosition=retractedPosition;

            board=GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name="Retractable board | steel backing";
            board.transform.SetParent(transform,false);
            board.transform.localScale=new Vector3(BoardWidth,BoardHeight,BoardThickness);
            var collider=board.GetComponent<Collider>();
            collider.enabled=false;
            Destroy(collider);
            boardMaterial=new Material(Shader.Find("Standard"));
            boardMaterial.color=new Color(.11f,.145f,.13f);
            boardMaterial.SetFloat("_Metallic",.55f);
            boardMaterial.SetFloat("_Glossiness",.35f);
            board.GetComponent<Renderer>().sharedMaterial=boardMaterial;

            var go=new GameObject("Interactive board face",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            go.transform.SetParent(transform,false);
            go.transform.localPosition=Vector3.back*(BoardThickness*.5f+.0015f);
            go.transform.localScale=Vector3.one*CanvasScale;
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
            raycaster=go.GetComponent<GraphicRaycaster>();
            ((RectTransform)go.transform).sizeDelta=new Vector2(CanvasWidth,CanvasHeight);
            aperture=Rect("Fixed wall opening",go.transform,new Rect(0,0,CanvasWidth,CanvasHeight));
            aperture.gameObject.AddComponent<RectMask2D>();
            viewport=Rect("Board controls",aperture,new Rect(0,0,CanvasWidth,CanvasHeight));
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},24);
            var background=Rect("Board display background",viewport,new Rect(0,0,CanvasWidth,CanvasHeight)).gameObject.AddComponent<Image>();
            background.color=new Color(.025f,.055f,.055f,1);
            background.raycastTarget=false;
            TextAt("FABRICATOR / MK.04   轮廓制造终端",new Rect(24,14,830,42),30);
            close=ButtonAt("返回 [Esc]",new Rect(928,16,148,42),()=>owner.Close());
            for(int i=0;i<choices.Length;i++)
            {
                int index=i;
                int y=88+i*110;
                choices[i]=ButtonAt(owner.Blueprints[i],new Rect(24,y,272,48),()=>owner.SelectBlueprint(index));
                counts[i]=TextAt("",new Rect(28,y+54,264,35),24);
            }
            var area=Rect("Blueprint viewport",viewport,new Rect(320,76,756,428));
            area.gameObject.AddComponent<RectMask2D>();
            var contourObject=Rect("Contour and fixed pen",area,new Rect(0,0,756,428)).gameObject;
            // Create the native renderer before Graphic.OnEnable can access it.
            contourObject.AddComponent<CanvasRenderer>();
            drawing=contourObject.AddComponent<FabricatorContourGraphic>();drawing.raycastTarget=false;
            status=TextAt("",new Rect(320,515,756,46),25);
            begin=ButtonAt("开始绘制图纸",new Rect(320,574,274,47),()=>owner.BeginDrawing());
            claim=ButtonAt("领取已完成部件",new Rect(24,574,272,47),()=>owner.ClaimParts());
            progress=TextAt("",new Rect(621,574,455,47),25);
            if(!FindObjectOfType<EventSystem>())
            {
                eventSystem=new GameObject("Fabricator pointer events",typeof(EventSystem),typeof(StandaloneInputModule));
                eventSystem.GetComponent<EventSystem>().sendNavigationEvents=false;
            }
            board.SetActive(false);
            go.SetActive(false);
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
            var t=Rect("Caption",rt,new Rect(8,0,rect.width-16,rect.height)).gameObject.AddComponent<Text>();t.font=font;t.fontSize=27;t.text=text;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(.8f,.94f,.88f);t.raycastTarget=false;return b;
        }
        public void Refresh(float deployment,bool visible,bool interactable)
        {
            if(!canvas)return;
            float raised=1-Mathf.SmoothStep(0,1,deployment);
            transform.localPosition=Vector3.Lerp(deployedPosition,retractedPosition,raised);
            float lift=raised*Travel;
            float height=BoardHeight-lift;
            // Draw only the part of the steel backing below the opening's upper edge.
            board.SetActive(visible&&height>0);
            if(board.activeSelf)
            {
                board.transform.localPosition=Vector3.down*(lift*.5f);
                board.transform.localScale=new Vector3(BoardWidth,height,BoardThickness);
            }
            // The aperture stays at the wall while the UI travels with the board.
            aperture.anchoredPosition=Vector2.down*(lift/CanvasScale);
            viewport.anchoredPosition=-aperture.anchoredPosition;
            canvas.gameObject.SetActive(visible&&lift<CanvasHeight*CanvasScale);
            raycaster.enabled=visible&&interactable;
            if(!visible)return;
            close.interactable=interactable;
            for(int i=0;i<choices.Length;i++)
            {
                bool available=owner.BlueprintAvailable(i);
                choices[i].gameObject.SetActive(available);
                counts[i].gameObject.SetActive(available);
                if(!available)continue;
                choices[i].interactable=interactable&&!owner.Printing&&!owner.Drawing;
                choices[i].GetComponent<Image>().color=owner.Selected==i?new Color(.2f,.4f,.34f):new Color(.14f,.22f,.21f);
                counts[i].text="可领取"+owner.Completed[i]+"/库存"+owner.DisplayStock(i);
            }
            begin.interactable=interactable&&!owner.Printing&&owner.BlueprintAvailable(owner.Selected);claim.interactable=interactable;
            begin.GetComponentInChildren<Text>().text=owner.Drawing?"重新开始":owner.Failed?"绘制失败 / 重试":"开始绘制图纸";
            status.text=owner.Message;
            progress.text=owner.Printing?"打印进度 "+Mathf.RoundToInt(owner.PrintProgress*100)+"%":owner.Drawing?"闭合进度 "+Mathf.RoundToInt(owner.TraceProgress/240*100)+"%   Q ↶ / E ↷":"Q / E 旋转 · Esc 返回";
            drawing.Set(owner.BlueprintAvailable(owner.Selected)?owner.Points:null,owner.Drawing,owner.TraceAngle,owner.TraceOffset,owner.TraceProgress);
        }
        void OnDestroy(){if(font)Destroy(font);if(boardMaterial)Destroy(boardMaterial);if(eventSystem)Destroy(eventSystem);}
    }
}
