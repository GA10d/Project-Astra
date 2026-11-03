using UnityEngine;

namespace AstraCabin
{
    // One application-wide frame budget, independent of scene and gameplay time scale.
    public sealed class CabinFramePacing : MonoBehaviour
    {
        int oldVSync, oldTarget;
        bool oldBackground;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var owner = new GameObject("Astra frame pacing");
            DontDestroyOnLoad(owner);
            owner.AddComponent<CabinFramePacing>();
        }
        void Awake()
        {
            oldVSync = QualitySettings.vSyncCount;
            oldTarget = Application.targetFrameRate;
            oldBackground = Application.runInBackground;
            // Explicit software cap: desktop Unity ignores targetFrameRate when vSync is enabled.
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true;
            Apply(Application.isFocused);
        }
        void OnApplicationFocus(bool focused) { Apply(focused); }
        void Apply(bool focused) { Application.targetFrameRate = focused ? 60 : 15; }
        void OnDestroy()
        {
            QualitySettings.vSyncCount = oldVSync;
            Application.targetFrameRate = oldTarget;
            Application.runInBackground = oldBackground;
        }
    }
}
