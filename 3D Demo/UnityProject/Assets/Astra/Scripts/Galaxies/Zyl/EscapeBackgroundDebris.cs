using System;
using UnityEngine;

namespace AstraCabin.Galaxies.Zyl
{
    // Authored pieces are baked from the refuge meshes in their original local space.
    sealed class EscapeBackgroundDebris : ScriptableObject
    {
        [Serializable]
        internal sealed class Piece
        {
            public Mesh mesh;
            public Vector3 pivot;
            public int objectIndex;
        }

        [Serializable]
        internal sealed class Group
        {
            public string objectName;
            public Mesh source;
            public Mesh stationary;
            public bool stone;
            public Piece[] pieces;
        }

        [SerializeField] internal Group[] groups;
    }
}
