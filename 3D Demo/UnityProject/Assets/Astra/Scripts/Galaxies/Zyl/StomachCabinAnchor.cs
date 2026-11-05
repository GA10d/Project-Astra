using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    /// <summary>Visual marker on the existing landing/settings object; local +X is the cabin window direction.</summary>
    [DisallowMultipleComponent]
    public sealed class StomachCabinAnchor : MonoBehaviour
    {
        public static StomachCabinAnchor Ensure(StomachEnvironmentSettings landing)
        {return landing.GetComponent<StomachCabinAnchor>()??landing.gameObject.AddComponent<StomachCabinAnchor>();}
        void OnDrawGizmos()
        {
            var matrix=Gizmos.matrix;var color=Gizmos.color;
            try
            {
                Gizmos.matrix=Matrix4x4.TRS(transform.position,transform.rotation,Vector3.one);
                Gizmos.color=new Color(.3f,1f,.55f);Gizmos.DrawWireCube(new Vector3(0,1.3f,0),new Vector3(3,2.6f,3));Gizmos.DrawWireSphere(Vector3.zero,.2f);
                Gizmos.color=new Color(1,.3f,.25f);var tip=new Vector3(3,1.3f,0);Gizmos.DrawLine(new Vector3(0,1.3f,0),tip);
                Gizmos.DrawLine(tip,tip+new Vector3(-.5f,0,.3f));Gizmos.DrawLine(tip,tip+new Vector3(-.5f,0,-.3f));
            }
            finally{Gizmos.matrix=matrix;Gizmos.color=color;}
        }
    }
}
