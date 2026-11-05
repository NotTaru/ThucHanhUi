using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FlowerEscape.EditorTools
{
    /// <summary>Adds and selects the two target resolutions in the Game view (9:21 phone, 3:4 tablet).</summary>
    public static class FlowerEscapeGameViewSizes
    {
        [MenuItem("Tools/Flower Escape/Game View 9-21 (1080x2520)", priority = 40)]
        public static void Phone() => Select("Flower 9:21", 1080, 2520);

        [MenuItem("Tools/Flower Escape/Game View 3-4 (1536x2048)", priority = 41)]
        public static void Tablet() => Select("Flower 3:4", 1536, 2048);

        public static void Select(string label, int w, int h)
        {
            try
            {
                var asm = typeof(Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes = single.GetProperty("instance").GetValue(null, null);
                var groupType = (GameViewSizeGroupType)sizesType.GetProperty("currentGroupType").GetValue(sizes, null);
                var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { (int)groupType });
                var gType = group.GetType();

                int index = Find(group, gType, w, h);
                if (index < 0)
                {
                    var sizeType = asm.GetType("UnityEditor.GameViewSize");
                    var kindType = asm.GetType("UnityEditor.GameViewSizeType");
                    var ctor = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) });
                    var size = ctor.Invoke(new object[] { Enum.ToObject(kindType, 1), w, h, label });
                    gType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    single.GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(sizes, null);
                    index = Find(group, gType, w, h);
                }

                var gvType = asm.GetType("UnityEditor.GameView");
                var gv = EditorWindow.GetWindow(gvType);
                var prop = gvType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                prop.SetValue(gv, index, null);
                gv.Repaint();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FlowerEscape] Could not set Game view size automatically ({e.Message}). Add {w}x{h} manually in the Game view resolution dropdown.");
            }
        }

        static int Find(object group, Type gType, int w, int h)
        {
            int count = (int)gType.GetMethod("GetTotalCount").Invoke(group, null);
            var get = gType.GetMethod("GetGameViewSize");
            for (int i = 0; i < count; i++)
            {
                var s = get.Invoke(group, new object[] { i });
                var st = s.GetType();
                if ((int)st.GetProperty("width").GetValue(s, null) == w && (int)st.GetProperty("height").GetValue(s, null) == h) return i;
            }
            return -1;
        }
    }
}
