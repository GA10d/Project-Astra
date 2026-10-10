using UnityEngine;

namespace Astra.PerformanceToolV2
{
    // Preserves material/property variants when the configured character is saved as a prefab.
    public sealed class ToolCharacterInstance : MonoBehaviour
    {
        public ToolCharacter preset;
        void Awake() { if(preset)ToolCharacterView.ApplyLook(gameObject,preset); }
    }
}
