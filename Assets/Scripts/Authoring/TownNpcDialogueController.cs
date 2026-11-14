using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    /// <summary>
    /// Handles nearby town NPC conversations using the serialized GameUI dialogue object.
    /// </summary>
    public sealed class TownNpcDialogueController : MonoBehaviour
    {
        public const float InteractionRange = 2.5f;

        readonly List<Transform> npcs = new List<Transform>();
        Dungeon game;
        TownNpcDialogueView view;
        Transform activeNpc;
        string[] activeLines;
        int lineIndex;
        int openedFrame = -1;

        public Transform NearbyNpc => FindNearestNpc();
        public string NearbyNpcName => DisplayName(NearbyNpc);

        public void Initialize(Dungeon owner, GameUIView view)
        {
            game = owner;
            if (game && game.Scene && game.Scene.Hirva)
                game.Scene.Hirva.GetComponentsInChildren<Transform>(true, npcs);

            if (!view)
            {
                Debug.LogError("Town NPC dialogue needs the GameUIView Canvas.", this);
                return;
            }

            this.view = view.GetComponentInChildren<TownNpcDialogueView>(true);
            if (!this.view)
            {
                Debug.LogError("NPC Dialogue Overlay is missing from the GameUI prefab. Run Western Lemegeton > Setup Town NPC Dialogue UI.", view);
                return;
            }

            this.view.SetFont(Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24));
            this.view.BindActions(Advance, Close);
            this.view.Hide();
        }

        public bool TryInteract()
        {
            if (!game || !game.IsTown || game.State != RunState.Town || !view) return false;
            var npc = FindNearestNpc();
            if (!npc) return false;

            activeNpc = npc;
            activeLines = LinesFor(npc);
            lineIndex = 0;
            openedFrame = Time.frameCount;
            game.State = RunState.Dialogue;
            RefreshDialogue();
            return true;
        }

        public void HandleInput()
        {
            if (game.State != RunState.Dialogue || Time.frameCount == openedFrame) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
            else if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) Advance();
        }

        public void Advance()
        {
            if (game.State != RunState.Dialogue) return;
            if (activeLines == null || lineIndex >= activeLines.Length - 1)
            {
                Close();
                return;
            }

            lineIndex++;
            RefreshDialogue();
        }

        public void Close()
        {
            if (view) view.Hide();
            activeNpc = null;
            activeLines = null;
            if (game && game.State == RunState.Dialogue) game.State = RunState.Town;
        }

        Transform FindNearestNpc()
        {
            if (!game || !game.Hero || !game.IsTown) return null;

            Transform nearest = null;
            float nearestDistance = InteractionRange;
            Vector3 player = game.Hero.transform.position;
            for (int i = 0; i < npcs.Count; i++)
            {
                var npc = npcs[i];
                if (!npc || !npc.gameObject.activeInHierarchy || !IsTownNpc(npc)) continue;
                Vector3 position = npc.position;
                float distance = Vector2.Distance(new Vector2(player.x, player.z), new Vector2(position.x, position.z));
                if (distance < nearestDistance)
                {
                    nearest = npc;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        static bool IsTownNpc(Transform candidate) => candidate.name.StartsWith("NPC - ");

        static string DisplayName(Transform npc)
        {
            if (npc.name.Contains("Dog Girl")) return "강아지 소녀";
            if (npc.name.Contains("Cat Girl")) return "고양이 소녀";
            if (npc.name.Contains("Sheep")) return "양치기";
            return "마을 사람";
        }

        static string[] LinesFor(Transform npc)
        {
            if (npc.name.Contains("Dog Girl"))
                return new[] { "안녕하세요! 이곳 히르바는 여행자들이 잠시 쉬어 가는 마을이에요.", "준비가 끝나면 마을 남쪽 출구를 통해 황야로 나가세요." };
            if (npc.name.Contains("Cat Girl"))
                return new[] { "황야의 방마다 다른 길과 사건이 기다리고 있어요.", "길이 헷갈리면 M 키를 눌러 탐험 지도를 확인해 보세요." };
            if (npc.name.Contains("Sheep"))
                return new[] { "문을 지나려면 그 방의 적을 모두 물리쳐야 해요.", "마지막 단계의 보스방까지 무사히 도착하길 바라요." };
            return new[] { "안녕하세요, 여행자님.", "황야로 떠나기 전에 마을을 둘러보세요." };
        }

        void RefreshDialogue()
        {
            if (!activeNpc) return;
            var source = activeNpc.GetComponent<SpriteRenderer>();
            string body = activeLines != null && lineIndex < activeLines.Length ? activeLines[lineIndex] : "";
            string continueCaption = activeLines != null && lineIndex >= activeLines.Length - 1 ? "대화 끝내기" : "계속  ·  F";
            view.Present(source ? source.sprite : null, DisplayName(activeNpc), body, continueCaption);
        }
    }
}
