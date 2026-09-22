using UnityEngine;

namespace AstraCabin
{
    public enum CabinSceneLook { Clear, Dithered, Pixel }

    [RequireComponent(typeof(Camera))]
    public sealed class CabinPresentation : MonoBehaviour
    {
        public Material grade;
        public bool computerBlur;
        public Shader blurShader;
        [Header("Industrial art sample")]
        public CabinSceneLook sceneLook = CabinSceneLook.Pixel;
        [Range(2,4)] public int pixelScale = 2;
        Material blur, runtimeGrade, gradeSource;

        public string LookLabel { get { return sceneLook == CabinSceneLook.Clear ? "清晰" : sceneLook == CabinSceneLook.Dithered ? "网点" : "复古像素"; } }
        public void CycleLook() { sceneLook = (CabinSceneLook)(((int)sceneLook + 1) % 3); }

        Material GradingMaterial()
        {
            if (gradeSource != grade)
            {
                if (runtimeGrade) Destroy(runtimeGrade);
                runtimeGrade = null; gradeSource = grade;
            }
            if (!grade || !grade.shader || !grade.shader.isSupported) return null;
            if (!runtimeGrade) runtimeGrade = new Material(grade) { hideFlags = HideFlags.HideAndDontSave };
            return runtimeGrade;
        }

        void Grade(RenderTexture source, RenderTexture destination, bool stylized)
        {
            var material = GradingMaterial();
            if (!material) { Graphics.Blit(source, destination); return; }
            int width = destination ? destination.width : source.width;
            int height = destination ? destination.height : source.height;
            material.SetVector("_OutputSize", new Vector4(width, height, 1f / width, 1f / height));
            material.SetFloat("_StyleStrength", stylized ? grade.GetFloat("_StyleStrength") : 0);
            material.SetFloat("_DitherSize", sceneLook == CabinSceneLook.Dithered ? 2 : 1);
            Graphics.Blit(source, destination, material);
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (computerBlur && blurShader && blurShader.isSupported)
            {
                if (!blur) blur = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
                var a = RenderTexture.GetTemporary(Mathf.Max(1, source.width / 4), Mathf.Max(1, source.height / 4), 0, source.format);
                var b = RenderTexture.GetTemporary(a.width, a.height, 0, source.format);
                try
                {
                    a.filterMode = b.filterMode = FilterMode.Bilinear;
                    a.wrapMode = b.wrapMode = TextureWrapMode.Clamp;
                    Graphics.Blit(source, a);
                    for (int i = 0; i < 3; i++)
                    {
                        blur.SetVector("_Direction", new Vector4(1f / a.width, 0, 0, 0)); Graphics.Blit(a, b, blur);
                        blur.SetVector("_Direction", new Vector4(0, 1f / a.height, 0, 0)); Graphics.Blit(b, a, blur);
                    }
                    // IMGUI draws the desktop later at native resolution.
                    Grade(a, destination, false);
                }
                finally { RenderTexture.ReleaseTemporary(a); RenderTexture.ReleaseTemporary(b); }
                return;
            }
            if (sceneLook != CabinSceneLook.Pixel)
            {
                Grade(source, destination, sceneLook == CabinSceneLook.Dithered);
                return;
            }
            int scale = Mathf.Clamp(pixelScale, 2, 4);
            var low = RenderTexture.GetTemporary(Mathf.Max(1, source.width / scale), Mathf.Max(1, source.height / scale), 0, source.format);
            var graded = RenderTexture.GetTemporary(low.width, low.height, 0, source.format);
            try
            {
                low.filterMode = FilterMode.Bilinear;
                graded.filterMode = FilterMode.Point;
                Graphics.Blit(source, low);
                Grade(low, graded, true);
                Graphics.Blit(graded, destination);
            }
            finally { RenderTexture.ReleaseTemporary(low); RenderTexture.ReleaseTemporary(graded); }
        }

        void OnDisable()
        {
            if (blur) Destroy(blur);
            if (runtimeGrade) Destroy(runtimeGrade);
            blur = null; runtimeGrade = null; gradeSource = null;
        }
    }
}
