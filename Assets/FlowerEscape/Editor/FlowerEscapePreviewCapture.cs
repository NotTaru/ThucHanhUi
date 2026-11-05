using System.IO;
using FlowerEscape.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FlowerEscape.EditorTools
{
    /// <summary>
    /// Renders both UI states at the two target aspect ratios (9:21 and 3:4) to PNG,
    /// so the responsive layout can be checked without switching the Game view by hand.
    /// Output: &lt;project&gt;/FigmaExport/Previews
    /// </summary>
    public static class FlowerEscapePreviewCapture
    {
        static readonly (string label, int w, int h)[] Sizes =
        {
            ("9x21", 1080, 2520),
            ("3x4", 1536, 2048),
            ("design_402x874", 804, 1748),
        };

        [MenuItem("Tools/Flower Escape/Capture Previews (9-21, 3-4)", priority = 20)]
        public static void CaptureAll()
        {
            var controller = Object.FindObjectOfType<FlowerEscapeUIController>();
            if (!controller) { Debug.LogError("Open the FlowerEscape_Gameplay scene first."); return; }
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "FigmaExport", "Previews");
            Directory.CreateDirectory(dir);
            foreach (var s in Sizes)
            foreach (FlowerEscapeUIController.State st in System.Enum.GetValues(typeof(FlowerEscapeUIController.State)))
            {
                controller.SetStateImmediate(st);
                File.WriteAllBytes(Path.Combine(dir, $"{st}_{s.label}.png"), Render(controller.GetComponent<Canvas>(), s.w, s.h));
            }
            controller.SetStateImmediate(controller.startState);
            Debug.Log("[FlowerEscape] Previews written to " + dir);
        }

        public static byte[] Render(Canvas canvas, int w, int h)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            var oldMode = canvas.renderMode;
            var oldCam = canvas.worldCamera;
            bool scalerWasEnabled = scaler && scaler.enabled;

            var camGo = new GameObject("__PreviewCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 100f; cam.enabled = false;
            camGo.transform.position = new Vector3(5000, 5000, -50);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;

            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
                if (scaler)
                {
                    var refRes = scaler.referenceResolution;
                    scaler.enabled = false;
                    canvas.scaleFactor = Mathf.Min(w / refRes.x, h / refRes.y); // == ScreenMatchMode.Expand
                }
                Canvas.ForceUpdateCanvases();
                foreach (var t in canvas.GetComponentsInChildren<TMPro.TMP_Text>()) t.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                var png = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);
                return png;
            }
            finally
            {
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCam;
                if (scaler) scaler.enabled = scalerWasEnabled;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGo);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
