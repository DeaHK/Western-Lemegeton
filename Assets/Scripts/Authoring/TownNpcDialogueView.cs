using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WesternLemegeton
{
    /// <summary>Serialized references for the NPC dialogue objects under the GameUI Canvas.</summary>
    public sealed class TownNpcDialogueView : MonoBehaviour
    {
        public Image Portrait;
        public Text Speaker;
        public Text Body;
        public Button ContinueButton;
        public Text ContinueLabel;
        public Button CloseButton;

        bool actionsBound;

        public void BindActions(UnityAction advance, UnityAction close)
        {
            if (actionsBound) return;
            if (ContinueButton) ContinueButton.onClick.AddListener(advance);
            if (CloseButton) CloseButton.onClick.AddListener(close);
            actionsBound = true;
        }

        public void SetFont(Font font)
        {
            foreach (var text in GetComponentsInChildren<Text>(true))
                if (text) text.font = font;
        }

        public void Present(Sprite portrait, string speaker, string body, string continueCaption)
        {
            if (Portrait)
            {
                Portrait.sprite = portrait;
                Portrait.enabled = portrait != null;
            }
            if (Speaker) Speaker.text = speaker;
            if (Body) Body.text = body;
            if (ContinueLabel) ContinueLabel.text = continueCaption;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
