using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    /// <summary>Authored root position of Mihuoer himself; independent of his craft. Blue +Z points toward the player cabin.</summary>
    [DisallowMultipleComponent]
    public sealed class StomachMihuoerAnchor : MonoBehaviour
    {
        public static StomachMihuoerAnchor Ensure(StomachEnvironmentSettings landing)
        {
            var existing=landing.gameObject.scene.GetRootGameObjects();
            foreach(var root in existing){var anchor=root.GetComponentInChildren<StomachMihuoerAnchor>(true);if(anchor)return anchor;}
            var go=new GameObject("Mihuoer initial position and facing | editable anchor");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,landing.gameObject.scene);
            var centre=landing.transform.position+Vector3.up*1.05f;
            var offset=landing.transform.rotation*Quaternion.Euler(0,70,0)*Vector3.right*6f;
            go.transform.SetPositionAndRotation(centre+offset,Quaternion.LookRotation(-offset.normalized,Vector3.up));
            return go.AddComponent<StomachMihuoerAnchor>();
        }
        void OnDrawGizmos(){Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(transform.position,.35f);Gizmos.DrawLine(transform.position,transform.position+transform.forward*2);}
    }
}
