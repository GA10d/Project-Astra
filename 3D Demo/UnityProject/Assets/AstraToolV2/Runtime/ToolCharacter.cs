using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Astra.PerformanceToolV2
{
    [CreateAssetMenu(menuName = "Astra/Performance Tool V2/Character")]
    public sealed class ToolCharacter : ScriptableObject
    {
        public string id = "lin";
        public string displayName = "林";
        public ToolSpecies species;
        public GameObject sourceModel;
        public ToolAvatarCatalog catalog;
        public ToolCharacterLook look = new ToolCharacterLook();
    }
}
