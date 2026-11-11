using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace WesternLemegeton.EditorTools
{
    /// <summary>Creates the editable dialogue hierarchy in the reusable GameUI prefab.</summary>
    public static class TownNpcDialogueUISetup
    {
        const string PrefabPath = "Assets/Editable/UI/GameUI.prefab";

        static readonly Color Ink = Hex("29223B");
        static readonly Color Paper = Hex("FFF9F1");
        static readonly Color Mint = Hex("B8E9E2");
        static readonly Color Blue = Hex("299CF2");
        static readonly Color Pink = Hex("F6CBE0");
        static readonly Color Orange = Hex("FA914A");

        [MenuItem("Western Lemegeton/Setup Town NPC Dialogue UI")]
        public static void Setup()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var old = root.transform.Find("NPC Dialogue Overlay");
                if (old) Object.DestroyImmediate(old.gameObject);
                Build(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Town NPC dialogue UI is saved under GameUI as an inactive prefab object.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Western Lemegeton/Update Town NPC Dialogue Button Labels")]
        public static void UpdateButtonLabels()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var closeLabel = FindDescendant(root.transform, "Close Dialogue Label");
                var continueLabel = FindDescendant(root.transform, "Continue Dialogue Label");
                var continueButton = FindDescendant(root.transform, "Continue Dialogue");
                if (!closeLabel || !continueLabel || !continueButton)
                {
                    Debug.LogError("Could not find NPC dialogue button labels in GameUI.prefab. Run Setup Town NPC Dialogue UI first.");
                    return;
                }

                SizeButtonLabel(closeLabel, new Vector2(34, 30));
                continueLabel.SetParent(continueButton, false);
                SizeButtonLabel(continueLabel, new Vector2(210, 46));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Town NPC dialogue button label sizes have been updated.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void SizeButtonLabel(Transform label, Vector2 size)
        {
            var rect = label.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        static Transform FindDescendant(Transform parent, string objectName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == objectName) return child;
                var nested = FindDescendant(child, objectName);
                if (nested) return nested;
            }
            return null;
        }

        static void Build(Transform parent)
        {
            var overlay = CreateRect("NPC Dialogue Overlay", parent, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            var shade = Box("Backdrop Shade", overlay, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(.055f, .035f, .085f, .58f));
            shade.raycastTarget = true;

            Box("Pixel Dialogue Window", overlay, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 42), new Vector2(1080, 250), Ink);
            var window = overlay.Find("Pixel Dialogue Window");
            var frame = StretchBox("Mint Pixel Frame", window, new Vector2(6, 6), new Vector2(-6, -6), Mint).transform;
            StretchBox("Inner Ink Frame", frame, new Vector2(5, 5), new Vector2(-5, -5), Ink);
            StretchBox("Paper Dialogue Surface", frame, new Vector2(9, 9), new Vector2(-9, -9), Paper);

            Box("Pink Pixel Corner", frame, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(18, 18), Pink);
            Box("Blue Pixel Corner", frame, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 12), new Vector2(18, 18), Blue);
            Box("Orange Pixel Accent", frame, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-62, -12), new Vector2(30, 18), Orange);

            var portraitFrame = Box("NPC Portrait Frame", frame, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(28, -4), new Vector2(166, 178), Pink).transform;
            var portraitBack = StretchBox("NPC Portrait Back", portraitFrame, new Vector2(6, 6), new Vector2(-6, -6), Blue).transform;
            var portrait = StretchBox("NPC Portrait", portraitBack, new Vector2(6, 6), new Vector2(-6, -6), Color.white);
            portrait.preserveAspect = true;

            var textArea = StretchRect("Dialogue Text Area", frame, new Vector2(224, 18), new Vector2(-242, -28));
            var speaker = Label("Speaker Name", textArea, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -10), new Vector2(560, 42), "마을 사람", 28, Ink, TextAnchor.MiddleLeft);
            Box("Speaker Name Underline", textArea, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -54), new Vector2(420, 5), Orange);
            var body = Label("Dialogue Text", textArea, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -68), new Vector2(600, 92), "", 25, Ink, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;

            var close = Button("Close Dialogue", frame, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(42, 38), Pink, "X", 21);
            var continueButton = Button("Continue Dialogue", frame, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-22, 18), new Vector2(218, 54), Blue, "계속  ·  F", 22);
            var continueLabel = continueButton.GetComponentInChildren<Text>();
            var controls = Label("Dialogue Controls", frame, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-252, 24), new Vector2(198, 40), "F 계속  ·  ESC 닫기", 14, Ink, TextAnchor.MiddleRight);
            controls.raycastTarget = false;

            var bindings = overlay.gameObject.AddComponent<TownNpcDialogueView>();
            bindings.Portrait = portrait;
            bindings.Speaker = speaker;
            bindings.Body = body;
            bindings.ContinueButton = continueButton;
            bindings.ContinueLabel = continueLabel;
            bindings.CloseButton = close;
            overlay.gameObject.SetActive(false);
        }

        static Button Button(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offset, Vector2 size, Color fill, string caption, int fontSize)
        {
            var image = Box(name, parent, anchorMin, anchorMax, pivot, offset, size, fill);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = fill;
            colors.highlightedColor = Color.Lerp(fill, Color.white, .18f);
            colors.pressedColor = Color.Lerp(fill, Ink, .18f);
            button.colors = colors;
            var labelSize = new Vector2(Mathf.Max(0, size.x - 8), Mathf.Max(0, size.y - 8));
            var label = Label(name + " Label", image.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, labelSize, caption, fontSize, Color.white, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            return button;
        }

        static Text Label(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offset, Vector2 size, string value, int fontSize, Color color, TextAnchor alignment)
        {
            var go = CreateRect(name, parent, anchorMin, anchorMax, pivot, offset, size).gameObject;
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        static Image Box(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offset, Vector2 size, Color color)
        {
            var image = CreateRect(name, parent, anchorMin, anchorMax, pivot, offset, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Image StretchBox(string name, Transform parent, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var image = Box(name, parent, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, color);
            var rect = (RectTransform)image.transform;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return image;
        }

        static RectTransform StretchRect(string name, Transform parent, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
