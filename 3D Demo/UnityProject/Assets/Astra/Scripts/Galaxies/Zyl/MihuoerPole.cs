using System;
using UnityEngine;

namespace AstraCabin.Galaxies.Zyl
{
    sealed class MihuoerPole : IDisposable
    {
        public const float PreviewLength = .62f;
        static readonly Vector3 SocketLocal = new Vector3(1.19f, .25f, -.48f);
        static readonly Vector3 AxisLocal = new Vector3(.64f, 1f, .14f).normalized;
        readonly Transform root, shaft, tip;
        readonly Material metal, collar;

        public static Vector3 Socket(Transform craft) => craft.TransformPoint(SocketLocal);
        public static Vector3 Axis(Transform craft) => craft.TransformDirection(AxisLocal);

        public MihuoerPole(Transform craft)
        {
            root = new GameObject("Mihuoer | telescoping pole").transform;
            root.SetParent(craft, false);
            root.SetPositionAndRotation(Socket(craft), Quaternion.FromToRotation(Vector3.up, Axis(craft)));
            // Keep authored world dimensions even on the scaled craft.
            root.localScale = Vector3.one / craft.lossyScale.x;
            metal = MakeMaterial(new Color(.32f, .35f, .31f), .78f);
            collar = MakeMaterial(new Color(.12f, .16f, .14f), .70f);
            Primitive("Pole base collar", PrimitiveType.Cylinder, collar).localScale = new Vector3(.44f, .22f, .44f);
            shaft = Primitive("Extending pole", PrimitiveType.Cylinder, metal);
            tip = Primitive("Rounded probing tip", PrimitiveType.Sphere, metal);
            tip.localScale = Vector3.one * .24f;
        }

        public void Sample(float length)
        {
            shaft.localPosition = Vector3.up * (length * .5f);
            shaft.localScale = new Vector3(.15f, length * .5f, .15f);
            tip.localPosition = Vector3.up * length;
        }

        Transform Primitive(string name, PrimitiveType shape, Material material)
        {
            var item = GameObject.CreatePrimitive(shape);
            item.name = name;
            item.layer = ZylStomachEnvironment.EnvironmentLayer;
            item.transform.SetParent(root, false);
            item.GetComponent<Renderer>().sharedMaterial = material;
            var collider = item.GetComponent<Collider>();
            collider.enabled = false;
            UnityEngine.Object.Destroy(collider);
            return item.transform;
        }

        static Material MakeMaterial(Color color, float metallic)
        {
            var material = new Material(Shader.Find("Standard")) { color = color };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", .55f);
            return material;
        }

        public void Dispose()
        {
            root.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(root.gameObject);
            UnityEngine.Object.Destroy(metal);
            UnityEngine.Object.Destroy(collar);
        }
    }
}
