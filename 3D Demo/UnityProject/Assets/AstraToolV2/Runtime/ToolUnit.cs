using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Astra.PerformanceToolV2
{
    [CreateAssetMenu(menuName = "Astra/Performance Tool V2/Unit")]
    public sealed class ToolUnit : ScriptableObject
    {
        public string id = "unit";
        public float duration = 8;
        public List<ToolActorSpan> actors = new List<ToolActorSpan>();
        public List<ToolActionSpan> actions = new List<ToolActionSpan>();
        public List<ToolDialogueSpan> dialogue = new List<ToolDialogueSpan>();
        public List<ToolSceneSpan> scenes = new List<ToolSceneSpan>();
        public List<ToolCameraSpan> cameras = new List<ToolCameraSpan>();
    }
}
