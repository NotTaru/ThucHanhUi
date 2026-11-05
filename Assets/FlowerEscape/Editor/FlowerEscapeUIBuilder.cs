using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowerEscape.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FlowerEscape.EditorTools
{
    /// <summary>
    /// Builds the Flower Escape gameplay UI (Buy Gear + Combat states) from the Figma export:
    ///  - slices FigmaExport/atlas@2x.png into individual sprites (deduplicated)
    ///  - creates the Lalezar TMP font asset + outline materials
    ///  - builds a responsive uGUI canvas (reference 402x874, Expand) and saves the scene.
    /// Menu: Tools > Flower Escape > Build UI
    /// </summary>
    public static class FlowerEscapeUIBuilder
    {
        public const string Root = "Assets/FlowerEscape";
        public const string SpritesFolder = Root + "/Sprites";
        const string FontsFolder = Root + "/Fonts";
        const string ScenePath = Root + "/Scenes/FlowerEscape_Gameplay.unity";
        const string LayoutPath = Root + "/Editor/figma_layout.json";
        const string TtfPath = FontsFolder + "/Lalezar-Regular.ttf";
        const string FontAssetPath = FontsFolder + "/Lalezar SDF.asset";

        static readonly Color Navy = new Color32(42, 60, 94, 255);

        // ---------------------------------------------------------------- data
        [Serializable] class Layout { public float designWidth; public float designHeight; public float atlasScale; public ScreenData[] screens; }
        [Serializable] class ScreenData { public string key; public El[] elements; }
        [Serializable]
        class El
        {
            public string id, name, kind, text, style, sprite, section, ha, va, button, parent;
            public float x, y, w, h, cx, cy, cw, ch, fs;
            public int z;
            public bool stretchX;
        }

        static float DW, DH;

        // ---------------------------------------------------------------- menu
        [MenuItem("Tools/Flower Escape/Build UI", priority = 0)]
        public static void BuildAll()
        {
            // Figma blends in sRGB; Gamma keeps semi-transparent UI and text outlines identical to the design.
            if (PlayerSettings.colorSpace != ColorSpace.Gamma) PlayerSettings.colorSpace = ColorSpace.Gamma;
            var layout = LoadLayout();
            var spriteMap = SliceSprites(layout);
            BuildScene(layout, spriteMap);
        }

        static Layout LoadLayout()
        {
            var json = File.ReadAllText(LayoutPath);
            var layout = JsonUtility.FromJson<Layout>(json);
            DW = layout.designWidth; DH = layout.designHeight;
            return layout;
        }

        static string ExportDir => Path.Combine(Directory.GetCurrentDirectory(), "FigmaExport");

        // ---------------------------------------------------------------- sprites
        static Dictionary<string, string> SliceSprites(Layout layout)
        {
            if (AssetDatabase.IsValidFolder(SpritesFolder)) AssetDatabase.DeleteAsset(SpritesFolder);
            EnsureFolder(SpritesFolder);
            var atlasBytes = File.ReadAllBytes(Path.Combine(ExportDir, "atlas@2x.png"));
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            atlas.LoadImage(atlasBytes);
            float s = layout.atlasScale;

            var map = new Dictionary<string, string>();       // element id -> asset path
            var byHash = new Dictionary<string, string>();    // pixel hash -> asset path
            var usedNames = new Dictionary<string, int>();
            var pixels = atlas.GetPixels32(0);
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                foreach (var scr in layout.screens)
                foreach (var e in scr.elements)
                {
                    if (e.kind != "image") continue;
                    int px = Mathf.RoundToInt(e.cx * s), pw = Mathf.RoundToInt(e.cw * s), ph = Mathf.RoundToInt(e.ch * s);
                    int py = atlas.height - Mathf.RoundToInt(e.cy * s) - ph;
                    px = Mathf.Clamp(px, 0, atlas.width - 1); py = Mathf.Clamp(py, 0, atlas.height - 1);
                    pw = Mathf.Min(pw, atlas.width - px); ph = Mathf.Min(ph, atlas.height - py);
                    var cut = new Color32[pw * ph];
                    for (int yy = 0; yy < ph; yy++)
                        Array.Copy(pixels, (py + yy) * atlas.width + px, cut, yy * pw, pw);
                    var raw = new byte[cut.Length * 4 + 8];
                    for (int i = 0; i < cut.Length; i++) { raw[i * 4] = cut[i].r; raw[i * 4 + 1] = cut[i].g; raw[i * 4 + 2] = cut[i].b; raw[i * 4 + 3] = cut[i].a; }
                    BitConverter.GetBytes(pw).CopyTo(raw, cut.Length * 4);
                    BitConverter.GetBytes(ph).CopyTo(raw, cut.Length * 4 + 4);
                    var hash = BitConverter.ToString(md5.ComputeHash(raw));
                    if (byHash.TryGetValue(hash, out var existing)) { map[e.id] = existing; continue; }

                    usedNames.TryGetValue(e.sprite, out int n);
                    usedNames[e.sprite] = n + 1;
                    var file = n == 0 ? e.sprite : $"{e.sprite}_{n + 1}";
                    var path = $"{SpritesFolder}/{file}.png";
                    var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, false);
                    tex.SetPixels32(cut); tex.Apply();
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    byHash[hash] = path; map[e.id] = path;
                }
            }
            Object.DestroyImmediate(atlas);

            File.Copy(Path.Combine(ExportDir, "environment_background@2x.png"), $"{SpritesFolder}/environment_background.png", true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[FlowerEscape] Sprites: {byHash.Count} unique from {map.Count} layers");
            return map;
        }

        // ---------------------------------------------------------------- fonts
        class Fonts { public TMP_FontAsset font; public Material plain, navyThick, navyThin, black; }

        static Fonts EnsureFonts()
        {
            if (Shader.Find("TextMeshPro/Distance Field") == null)
                throw new Exception("TMP Essential Resources missing: Window > TextMeshPro > Import TMP Essential Resources, then run again.");

            // Baked once as a Static atlas, so glyphs can never be lost on play mode / reimport.
            // Delete the asset to force regeneration.
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null && existing.atlasPopulationMode == AtlasPopulationMode.Static && existing.characterTable.Count > 0)
                return MakeFonts(existing);
            if (existing != null) AssetDatabase.DeleteAsset(FontAssetPath);
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            var fa = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            fa.name = "Lalezar SDF";
            fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");
            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(fa, FontAssetPath);
            fa.material.name = "Lalezar SDF Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.atlasTextures[0].name = "Lalezar SDF Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            EditorUtility.SetDirty(fa.atlasTextures[0]);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceSynchronousImport);
            fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            return MakeFonts(fa);
        }

        static Fonts MakeFonts(TMP_FontAsset fa)
        {
            return new Fonts
            {
                font = fa,
                plain = fa.material,
                navyThick = OutlineMat("Lalezar SDF - Outline Navy Thick", fa, Navy, 0.32f),
                navyThin = OutlineMat("Lalezar SDF - Outline Navy Thin", fa, Navy, 0.3f),
                black = OutlineMat("Lalezar SDF - Outline Black", fa, Color.black, 0.28f),
            };
        }

        static Material OutlineMat(string name, TMP_FontAsset fa, Color color, float width)
        {
            var path = $"{FontsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(fa.material) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find("TextMeshPro/Distance Field");
            mat.CopyPropertiesFromMaterial(fa.material);
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, color);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, width * 0.5f);
            ShaderUtilities.UpdateShaderRatios(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- scene
        static void BuildScene(Layout layout, Dictionary<string, string> sprites)
        {
            EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene unloads unreferenced assets, so fonts/materials are (re)loaded only after it.
            var fonts = EnsureFonts();

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(24, 74, 120, 255);
            cam.orthographic = true;
            camGo.transform.position = new Vector3(0, 0, -10);

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // Canvas: design 402x874; Expand => 9:21 grows taller, 3:4 grows wider
            var canvasGo = new GameObject("UI_FlowerEscape", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DW, DH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100;

            var safe = Stretch(NewRect("SafeArea", canvasGo.transform));
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // shared background (bleeds on wider/taller screens)
            var bgPath = $"{SpritesFolder}/environment_background.png";
            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(bgPath);
            var fill = Stretch(NewRect("BackgroundFill", canvasGo.transform));
            fill.SetAsFirstSibling();
            AddImage(fill.gameObject, null, BottomColor(bgSprite));
            var bg = NewRect("EnvironmentBackground", canvasGo.transform);
            bg.SetSiblingIndex(1);
            AddImage(bg.gameObject, bgSprite, Color.white);
            bg.anchorMin = bg.anchorMax = new Vector2(0.5f, 1f);
            bg.pivot = new Vector2(0.5f, 1f);
            bg.sizeDelta = new Vector2(874, 874);
            bg.anchoredPosition = Vector2.zero;
            bg.gameObject.AddComponent<BackgroundCover>();
            safe.SetAsLastSibling();

            var controller = canvasGo.AddComponent<FlowerEscapeUIController>();
            var speedLabels = new List<TMP_Text>();
            var stateGroups = new Dictionary<string, CanvasGroup>();

            foreach (var scr in layout.screens)
            {
                var stateRt = Stretch(NewRect("State_" + scr.key, safe));
                var group = stateRt.gameObject.AddComponent<CanvasGroup>();
                stateGroups[scr.key] = group;

                var sections = new Dictionary<string, RectTransform>();
                var nodes = new Dictionary<string, RectTransform>();
                var byId = scr.elements.ToDictionary(e => e.id);

                foreach (var e in scr.elements.OrderBy(e => e.z))
                {
                    if (!sections.TryGetValue(e.section, out var sec))
                        sections[e.section] = sec = Stretch(NewRect(e.section, stateRt));

                    RectTransform parent = sec;
                    El parentEl = null;
                    if (!string.IsNullOrEmpty(e.parent) && nodes.TryGetValue(e.parent, out var p)) { parent = p; parentEl = byId[e.parent]; }

                    string goName = e.kind == "text" ? $"Txt_{e.name}" : (string.IsNullOrEmpty(e.button) ? e.name : $"Btn_{e.button}");
                    var rt = NewRect(goName, parent);
                    if (parentEl != null)
                        Place(rt, e.x - parentEl.x, e.y - parentEl.y, e.w, e.h, "L", "T", parentEl.w, parentEl.h, false);
                    else
                        Place(rt, e.x, e.y, e.w, e.h, e.ha, e.va, DW, DH, e.stretchX);

                    if (e.kind == "text")
                    {
                        var t = AddText(rt.gameObject, e, fonts);
                        if (parentEl != null && parentEl.button == "Speed") speedLabels.Add(t);
                    }
                    else
                    {
                        var sp = sprites.TryGetValue(e.id, out var path) ? AssetDatabase.LoadAssetAtPath<Sprite>(path) : null;
                        var img = AddImage(rt.gameObject, sp, Color.white);
                        if (!string.IsNullOrEmpty(e.button))
                        {
                            img.raycastTarget = true;
                            var btn = rt.gameObject.AddComponent<Button>();
                            btn.transition = Selectable.Transition.ColorTint;
                            var colors = btn.colors; colors.pressedColor = new Color(0.85f, 0.85f, 0.85f); btn.colors = colors;
                            rt.gameObject.AddComponent<UIPressScale>();
                            WireButton(btn, scr.key, e.button, controller);
                        }
                    }
                    nodes[e.id] = rt;
                }
            }

            // ad banner placeholder shared by both states (bottom, full width, 96 high)
            var ad = NewRect("AdBanner_Placeholder", safe);
            ad.anchorMin = new Vector2(0, 0); ad.anchorMax = new Vector2(1, 0); ad.pivot = new Vector2(0.5f, 0);
            ad.sizeDelta = new Vector2(0, 96); ad.anchoredPosition = Vector2.zero;
            AddImage(ad.gameObject, null, new Color32(20, 22, 30, 255)).raycastTarget = true;
            var adText = NewRect("Txt_AdBanner", ad);
            Stretch(adText);
            var adTmp = adText.gameObject.AddComponent<TextMeshProUGUI>();
            adTmp.font = fonts.font; adTmp.fontSharedMaterial = fonts.plain; adTmp.text = "AD BANNER";
            adTmp.fontSize = 18; adTmp.color = new Color(1, 1, 1, 0.35f); adTmp.alignment = TextAlignmentOptions.Center; adTmp.raycastTarget = false;

            controller.buyGearState = stateGroups["BuyGear"];
            controller.combatState = stateGroups["Combat"];
            controller.speedLabels = speedLabels.ToArray();
            controller.startState = FlowerEscapeUIController.State.BuyGear;
            controller.SetStateImmediate(FlowerEscapeUIController.State.BuyGear);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[FlowerEscape] Scene built: " + ScenePath);
        }

        static void WireButton(Button btn, string screen, string button, FlowerEscapeUIController c)
        {
            switch (button)
            {
                case "Start": UnityEventTools.AddPersistentListener(btn.onClick, c.ShowCombat); break;
                case "Close" when screen == "Combat": UnityEventTools.AddPersistentListener(btn.onClick, c.ShowBuyGear); break;
                case "Speed": UnityEventTools.AddPersistentListener(btn.onClick, c.ToggleSpeed); break;
            }
        }

        // ---------------------------------------------------------------- helpers
        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        /// <summary>Places a Figma rect (top-left origin) inside a parent of size pw x ph.
        /// ha: L/C/R horizontal anchor, va: T/B vertical anchor.</summary>
        static void Place(RectTransform rt, float x, float y, float w, float h, string ha, string va, float pw, float ph, bool stretchX)
        {
            float ax = ha == "L" ? 0f : ha == "R" ? 1f : 0.5f;
            float ay = va == "T" ? 1f : 0f;
            rt.pivot = new Vector2(0.5f, 0.5f);
            float cx = x + w * 0.5f, cy = ph - (y + h * 0.5f);
            if (stretchX)
            {
                rt.anchorMin = new Vector2(0, ay); rt.anchorMax = new Vector2(1, ay);
                rt.sizeDelta = new Vector2(w - pw, h);
                rt.anchoredPosition = new Vector2(cx - pw * 0.5f, cy - ay * ph);
                return;
            }
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(cx - ax * pw, cy - ay * ph);
        }

        static Image AddImage(GameObject go, Sprite sprite, Color color)
        {
            var img = go.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            img.type = Image.Type.Simple; img.preserveAspect = false;
            return img;
        }

        static TextMeshProUGUI AddText(GameObject go, El e, Fonts f)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = f.font;
            t.text = e.text;
            t.fontSize = e.fs;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.margin = Vector4.zero;
            switch (e.style)
            {
                case "N": t.color = Color.white; t.fontSharedMaterial = e.fs < 22 ? f.navyThick : f.navyThin; break;
                case "K": t.color = Color.white; t.fontSharedMaterial = f.black; break;
                case "C": t.color = new Color32(65, 226, 255, 255); t.fontSharedMaterial = f.black; break;
                case "Y": t.color = new Color32(255, 229, 97, 255); t.fontSharedMaterial = f.plain; break;
                default: t.color = Color.white; t.fontSharedMaterial = f.plain; break;
            }
            return t;
        }

        static Color BottomColor(Sprite s)
        {
            if (s == null) return new Color32(24, 74, 120, 255);
            var path = AssetDatabase.GetAssetPath(s);
            var tex = new Texture2D(2, 2); tex.LoadImage(File.ReadAllBytes(path));
            Color sum = Color.clear; int n = 0;
            for (int x = 0; x < tex.width; x += 8) { sum += tex.GetPixel(x, 2); n++; }
            Object.DestroyImmediate(tex);
            var c = sum / Mathf.Max(1, n); c.a = 1; return c;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
