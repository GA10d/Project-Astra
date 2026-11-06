using UnityEngine;
using UnityEngine.UI;
namespace AstraCabin
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class FabricatorContourGraphic : MaskableGraphic
    {
        Vector2[] points;bool tracing;float angle,progress;Vector2 offset;
        public void Set(Vector2[] path,bool active,float rotation,Vector2 translation,float p){points=path;tracing=active;angle=rotation;offset=translation;progress=p;SetVerticesDirty();}
        Vector2 Rotate(Vector2 p){float a=angle*Mathf.Deg2Rad;return new Vector2(p.x*Mathf.Cos(a)-p.y*Mathf.Sin(a),p.x*Mathf.Sin(a)+p.y*Mathf.Cos(a));}
        void Segment(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int k=vh.currentVertCount;
            vh.AddVert(a-n,color,Vector2.zero);vh.AddVert(a+n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);
            vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float w=rectTransform.rect.width,h=rectTransform.rect.height;
            // Top-left coordinates agree with panel layout; all lines live in this local viewport.
            int k=vh.currentVertCount;Color bg=new Color(.045f,.085f,.085f);
            vh.AddVert(new Vector3(0,0),bg,Vector2.zero);vh.AddVert(new Vector3(w,0),bg,Vector2.zero);vh.AddVert(new Vector3(w,-h),bg,Vector2.zero);vh.AddVert(new Vector3(0,-h),bg,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
            for(float x=0;x<=w;x+=30)Segment(vh,new Vector2(x,0),new Vector2(x,-h),1,new Color(.09f,.16f,.16f));
            for(float y=0;y<=h;y+=30)Segment(vh,new Vector2(0,-y),new Vector2(w,-y),1,new Color(.09f,.16f,.16f));
            if(points==null)return;
            Vector2 min=points[0],max=points[0];foreach(var p in points){min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            // Centre the complete blueprint, rather than putting its start at an arbitrary left offset.
            Vector2 pen=new Vector2(w*.5f,h*.5f)-(min+max)*.5f;
            for(int i=0;i<points.Length-1;i++)
            {
                Vector2 a=pen+(tracing?Rotate(points[i])+offset:points[i]),b=pen+(tracing?Rotate(points[i+1])+offset:points[i+1]);a.y=-a.y;b.y=-b.y;
                Segment(vh,a,b,9,new Color(.015f,.04f,.035f));
                Segment(vh,a,b,6,tracing&&i<progress?new Color(1,.76f,.25f):new Color(.4f,.86f,.79f));
            }
            if(tracing){pen.y=-pen.y;Segment(vh,pen+Vector2.left*16,pen+Vector2.right*16,3,Color.white);Segment(vh,pen+Vector2.up*16,pen+Vector2.down*16,3,Color.white);}
        }
    }
}
