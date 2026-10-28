using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    // 선 전체를 작은 Enemy 판정들로 덮어 기존 Hunter/까마귀 공격이 밧줄을 맞힐 수 있게 합니다.
    [DefaultExecutionOrder(100)]
    public sealed class CowboyRope : MonoBehaviour
    {
        CowboyAttack cowboy;
        Player hunter;
        SpriteRenderer line;
        readonly List<Enemy> targets = new List<Enemy>();
        float pullSpeed, maxLength;
        public bool IsBound { get; private set; }
        public IReadOnlyList<Enemy> Targets => targets;

        // 기존 구속을 해제하고 양쪽 참조를 연결한 뒤, 밧줄을 공격할 수 있는 보조 적 판정을 생성합니다.
        public static CowboyRope Bind(CowboyAttack cowboy, Player hunter, float speed, float maxLength)
        {
            if (cowboy.Rope) cowboy.Rope.Release();
            if (hunter.Tether) hunter.Tether.Release();
            var root = new GameObject("Cowboy / Breakable rope");
            var rope = root.AddComponent<CowboyRope>();
            rope.cowboy = cowboy; rope.hunter = hunter; rope.pullSpeed = speed; rope.maxLength = maxLength;
            rope.IsBound = true; hunter.Tether = rope; cowboy.Rope = rope;
            rope.line = Ink.Shape("Rope", root.transform, Vector2.zero, Vector2.one, Ink.Gold, -575);
            int count = Mathf.Max(2, Mathf.CeilToInt(maxLength / .6f));
            for (int i = 0; i < count; i++)
            {
                var part = new GameObject("Rope target " + i);
                part.transform.SetParent(root.transform, false);
                var enemy = part.AddComponent<Enemy>();
                enemy.AuxiliaryTarget = true; enemy.Hp = enemy.MaxHp = 1;
                enemy.Damaged += rope.OnHit;
                rope.targets.Add(enemy); Dungeon.I.QueueEnemy(enemy);
            }
            rope.Refresh();
            return rope;
        }

        // 구속 중 Hunter를 작은 단계로 끌어 장애물을 통과하지 않도록 합니다.
        public void Pull(float dt)
        {
            if (!Valid()) { Release(); return; }
            Vector2 p = hunter.transform.position;
            Vector2 goal = Vector2.MoveTowards(p, cowboy.transform.position, pullSpeed * dt);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p, goal) / .1f));
            for (int i = 1; i <= steps; i++)
                hunter.transform.position = Rules.ResolveObstacles(hunter.transform.position, Vector2.Lerp(p, goal, i / (float)steps), .38f);
        }
        // 생존·활성 여부와 최대 길이를 검사하며, 게임 진행 중에만 밧줄 배치를 갱신합니다.
        bool Valid() => IsBound && hunter && hunter.Hp > 0 && cowboy && cowboy.Owner && !cowboy.Owner.Dead &&
            cowboy.gameObject.activeInHierarchy && Vector2.Distance(hunter.transform.position, cowboy.transform.position) <= maxLength;
        void LateUpdate()
        {
            if (!Valid()) { Release(); return; }
            if (Dungeon.I && Dungeon.I.Running) Refresh();
        }
        // 양 끝 위치에 맞춰 선을 회전·확대하고 보조 적 판정을 선 전체에 고르게 배치합니다.
        public void Refresh()
        {
            Vector2 a = cowboy.transform.position, b = hunter.transform.position, delta = b - a;
            line.transform.position = (a + b) * .5f;
            line.transform.localScale = new Vector3(delta.magnitude, .09f, 1);
            line.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            for (int i = 0; i < targets.Count; i++)
                if (targets[i]) targets[i].transform.position = Vector2.Lerp(a, b, (i + .5f) / targets.Count);
        }
        // 한 부분이라도 맞으면 구속 전체를 해제하고 보조 적 판정과 표시를 정리합니다.
        void OnHit(Enemy enemy) => Release();
        public void Release()
        {
            if (!IsBound) return;
            IsBound = false;
            if (hunter && hunter.Tether == this) hunter.Tether = null;
            if (cowboy && cowboy.Rope == this) cowboy.Rope = null;
            foreach (var enemy in targets)
                if (enemy) { enemy.Damaged -= OnHit; enemy.Hp = 0; }
            // foreach로 적을 공격하는 도중 리스트를 제거하지 않습니다.
            if (line) line.enabled = false;
            RemoveObject(gameObject);
        }
        public static void RemoveObject(GameObject target)
        {
            if (!target) return;
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
        // 외부에서 오브젝트가 제거된 경우에도 양쪽의 구속 참조를 남기지 않습니다.
        void OnDestroy()
        {
            IsBound = false;
            if (hunter && hunter.Tether == this) hunter.Tether = null;
            if (cowboy && cowboy.Rope == this) cowboy.Rope = null;
        }
    }
}
