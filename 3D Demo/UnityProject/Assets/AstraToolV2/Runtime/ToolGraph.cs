using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Astra.PerformanceToolV2
{
    [CreateAssetMenu(menuName = "Astra/Performance Tool V2/Graph")]
    public sealed class ToolGraph : ScriptableObject
    {
        public string entryNode;
        public List<ToolGraphNode> nodes = new List<ToolGraphNode>();
        public ToolGraphNode Node(string id) { foreach (var n in nodes) if (n.id == id) return n; return null; }
    }
}
