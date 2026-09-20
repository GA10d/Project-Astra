using UnityEngine;

namespace AstraCabin
{
    [RequireComponent(typeof(Camera))]
    public sealed class CabinPresentation : MonoBehaviour
    {
        public Material grade;
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (grade != null) Graphics.Blit(source, destination, grade);
            else Graphics.Blit(source, destination);
        }
    }
}
