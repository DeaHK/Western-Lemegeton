using UnityEngine;

namespace WesternLemegeton
{
    // PaperCard의 지면 재투영을 거치지 않는 실제 3D 총격 선/피격 이펙트입니다.
    public sealed class WorldShotEffect : MonoBehaviour
    {
        static Material material;
        LineRenderer line;
        Color color;
        float remaining, duration;
        public static void Create(Vector3 from, Vector3 to, Color color, float width, float duration)
        {
            if (!material) material = new Material(Shader.Find("Sprites/Default"));
            var effect = new GameObject("World shot effect").AddComponent<WorldShotEffect>();
            effect.line = effect.gameObject.AddComponent<LineRenderer>();
            effect.line.sharedMaterial = material;
            effect.line.useWorldSpace = true; effect.line.positionCount = 2;
            effect.line.SetPosition(0, from); effect.line.SetPosition(1, to);
            effect.line.startWidth = effect.line.endWidth = width;
            effect.line.startColor = effect.line.endColor = color;
            effect.line.sortingOrder = 10010;
            effect.color = color; effect.remaining = effect.duration = duration;
        }
        void Update()
        {
            if (!Dungeon.I || !Dungeon.I.Running) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0) { Destroy(gameObject); return; }
            var tint = color; tint.a *= remaining/duration;
            line.startColor = line.endColor = tint;
        }
    }
}
