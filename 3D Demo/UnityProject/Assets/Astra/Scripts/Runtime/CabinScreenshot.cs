using System;
using System.IO;
using System.Collections;
using UnityEngine;

namespace AstraCabin
{
    // F12 captures the actual window, including IMGUI. This is deliberately separate
    // from the QA offscreen camera render, which cannot include the desktop GUI.
    public sealed class CabinScreenshot : MonoBehaviour
    {
        bool capturing;
        void Update() { if (!capturing && Input.GetKeyDown(KeyCode.F12)) StartCoroutine(Capture()); }
        IEnumerator Capture()
        {
            capturing = true;
            yield return new WaitForEndOfFrame();
            var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0); image.Apply();
            var directory = Path.Combine(Application.persistentDataPath,"Screenshots"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory,"Astra_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".png");
            File.WriteAllBytes(path,image.EncodeToPNG()); Destroy(image); capturing = false;
            Debug.Log("ASTRA_SCREENSHOT "+path);
        }
    }
}
