using UnityEngine;
namespace AstraCabin.Galaxies.Zyl
{
    /// <summary>Independent authored craft pose; moving this anchor does not move Mihuoer.</summary>
    [DisallowMultipleComponent]
    public sealed class StomachMihuoerShipAnchor : MonoBehaviour
    {
        public static StomachMihuoerShipAnchor Ensure(StomachEnvironmentSettings landing,StomachMihuoerAnchor character)
        {
            foreach(var root in landing.gameObject.scene.GetRootGameObjects())
            {var existing=root.GetComponentInChildren<StomachMihuoerShipAnchor>(true);if(existing)return existing;}
            var go=new GameObject("Mihuoer ship initial position and facing | editable anchor");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,landing.gameObject.scene);
            // Set a separate starting pose only on creation. Authored poses are never overwritten.
            go.transform.SetPositionAndRotation(character.transform.position+character.transform.right*4.5f,character.transform.rotation*Quaternion.Euler(0,180,0));
            return go.AddComponent<StomachMihuoerShipAnchor>();
        }
        void OnDrawGizmos(){Gizmos.color=new Color(1,.7f,.15f);Gizmos.DrawWireCube(transform.position,new Vector3(3.4f,.5f,3));Gizmos.DrawLine(transform.position,transform.position+transform.forward*2);}
    }
}
