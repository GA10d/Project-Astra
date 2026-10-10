using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Astra.PerformanceToolV2
{
    [CreateAssetMenu(menuName = "Astra/Performance Tool V2/Library")]
    public sealed class ToolLibrary : ScriptableObject
    {
        public ToolCharacter[] characters = Array.Empty<ToolCharacter>();
        public ToolAction[] actions = Array.Empty<ToolAction>();
        public ToolCameraPreset[] cameras = Array.Empty<ToolCameraPreset>();
        public ToolScenePreset[] scenes = Array.Empty<ToolScenePreset>();
        public ToolAction Action(string id) { foreach (var a in actions) if (a.id == id) return a; return null; }
        public ToolScenePreset Scene(string id) { foreach (var s in scenes) if (s.id == id) return s; return null; }
        public ToolCharacter Character(string id) { foreach (var c in characters) if (c && c.id == id) return c; return null; }
    }
}
