using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace WesternLemegeton
{
    public sealed class PassiveSlotView : MonoBehaviour, IPointerClickHandler
    {
        public Image Icon, Border;
        public Text Tooltip;
        public string Description;
        public Action<PassiveSlotView> Selected;
        Text count;

        public void SetStack(int stack)
        {
            if (!count)
            {
                count = new GameObject("Stack", typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
                var rect = count.rectTransform;
                rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(1, 1);
                rect.offsetMax = new Vector2(-2, -1);
                count.font = Tooltip.font;
                count.fontSize = 16;
                count.alignment = TextAnchor.LowerRight;
                count.raycastTarget = false;
                count.GetComponent<Outline>().effectColor = Color.black;
            }
            count.text = stack.ToString();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Selected?.Invoke(this);
        }
    }
}
