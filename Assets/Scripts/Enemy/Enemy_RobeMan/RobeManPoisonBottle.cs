using UnityEngine;

namespace WesternLemegeton
{
    public sealed class RobeManPoisonBottle : MonoBehaviour
    {
        Vector2 start, target;
        float elapsed, duration, arcHeight, radius, lifetime, damage, interval;
        PaperCard card;
        SpriteRenderer marker;
        bool landed;
        public Vector2 Target => target;
        public float VisualHeight { get; private set; }

        // 발사 시 목표 지점과 독 성능을 복사하고 조준 표시의 정리 책임을 넘겨받습니다.
        public static RobeManPoisonBottle Spawn(Vector2 start, Vector2 target, float duration, float arcHeight,
            float radius, float lifetime, float damage, float interval, SpriteRenderer marker)
        {
            var sprite = Ink.Shape("RobeMan / Poison bottle", null, start, new Vector2(.28f, .4f),
                new Color(.4f, .9f, .25f), 1000);
            var bottle = sprite.gameObject.AddComponent<RobeManPoisonBottle>();
            bottle.start = start; bottle.target = target;
            bottle.duration = Mathf.Max(.01f, duration); bottle.arcHeight = Mathf.Max(0, arcHeight);
            bottle.radius = radius; bottle.lifetime = lifetime; bottle.damage = damage; bottle.interval = interval;
            bottle.marker = marker;
            bottle.card = sprite.GetComponent<PaperCard>();
            return bottle;
        }

        // 게임 진행 중에만 비행 시간을 누적하여 일시 정지와 컷신 중에는 멈춥니다.
        void Update()
        {
            if (Dungeon.I && Dungeon.I.Running) Advance(Time.deltaTime);
        }

        // 고정 목표로 이동하며 포물선 높이를 표현하고, 도착하면 독 구역을 생성한 뒤 병과 예고를 제거합니다.
        public void Advance(float dt)
        {
            if (landed) return;
            elapsed += Mathf.Max(0, dt);
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector2.Lerp(start, target, t);
            // 논리 위치는 평면 위를 이동하고, 렌더링 높이에만 포물선을 적용합니다.
            VisualHeight = 4 * arcHeight * t * (1 - t);
            if (card) card.RenderOffset = Vector3.up * VisualHeight;
            if (t < 1) return;
            landed = true;
            RobeManPoisonZone.Spawn(target, radius, lifetime, damage, interval);
            if (marker) RobeManPoisonZone.Remove(marker.gameObject);
            marker = null;
            RobeManPoisonZone.Remove(gameObject);
        }

        // 착지 전에 외부에서 제거되어도 남아 있는 조준 표시를 함께 정리합니다.
        void OnDestroy()
        {
            if (marker) RobeManPoisonZone.Remove(marker.gameObject);
        }
    }
}
