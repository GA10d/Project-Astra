using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Astra.PerformanceToolV2
{
    [CreateAssetMenu(menuName = "Astra/Performance Tool V2/Package")]
    public sealed class ToolPackage : ScriptableObject
    {
        public ToolLibrary library;
        public ToolGraph graph;
        public ToolStat[] initialStats = Array.Empty<ToolStat>();
        public int initialPatience = 10;
    }
}
