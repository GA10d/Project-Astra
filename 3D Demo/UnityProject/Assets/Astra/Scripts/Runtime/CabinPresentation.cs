using UnityEngine;

namespace AstraCabin
{
    [RequireComponent(typeof(Camera))]
    public sealed class CabinPresentation : MonoBehaviour
    {
        public Material grade;
        public bool computerBlur;
        public Shader blurShader;
        Material blur;
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!computerBlur || !blurShader)
            {
                if (grade != null) Graphics.Blit(source, destination, grade);
                else Graphics.Blit(source, destination);
                return;
            }
            if (!blur) blur = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
            var a = RenderTexture.GetTemporary(Mathf.Max(1, source.width / 4), Mathf.Max(1, source.height / 4), 0, source.format);
            var b = RenderTexture.GetTemporary(a.width, a.height, 0, source.format);
            Graphics.Blit(source, a);
            for (int i = 0; i < 3; i++)
            {
                blur.SetVector("_Direction", new Vector4(1f / a.width, 0, 0, 0)); Graphics.Blit(a, b, blur);
                blur.SetVector("_Direction", new Vector4(0, 1f / a.height, 0, 0)); Graphics.Blit(b, a, blur);
            }
            if (grade) Graphics.Blit(a, destination, grade); else Graphics.Blit(a, destination);
            RenderTexture.ReleaseTemporary(a); RenderTexture.ReleaseTemporary(b);
        }
        void OnDestroy() { if (blur) Destroy(blur); }
    }
}
