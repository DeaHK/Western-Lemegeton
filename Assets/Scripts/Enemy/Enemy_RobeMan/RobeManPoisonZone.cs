using UnityEngine;
using System.Collections.Generic;

namespace WesternLemegeton
{
    // Collider를 만들지 않으므로 자유롭게 통과할 수 있는 원형 위험구역입니다.
    public sealed class RobeManPoisonZone : MonoBehaviour
    {
        float remaining, insideTime;
        bool expired;
        readonly Dictionary<Enemy, float> enemyExposure = new Dictionary<Enemy, float>();
        readonly List<Enemy> enemySnapshot = new List<Enemy>();
        readonly List<Enemy> previousTargets = new List<Enemy>();
        public float Radius { get; private set; }
        public float Damage { get; private set; }
        public float Interval { get; private set; }

        // 원형 표시와 독 성능을 생성 시 고정하고 유효한 최소 반경·수명·피해 간격을 보장합니다.
        public static RobeManPoisonZone Spawn(Vector2 center, float radius, float lifetime, float damage, float interval)
        {
            var circle = CreateCircle("RobeMan / Poison danger zone", center, radius, new Color(.3f, .8f, .12f, .4f));
            var zone = circle.gameObject.AddComponent<RobeManPoisonZone>();
            zone.Radius = Mathf.Max(.01f, radius);
            zone.remaining = Mathf.Max(.01f, lifetime);
            zone.Damage = Mathf.Max(0, damage);
            zone.Interval = Mathf.Max(.01f, interval);
            return zone;
        }

        // 조준과 독 구역이 공유하는 원형 표시를 월드 반경의 두 배 크기로 생성합니다.
        public static SpriteRenderer CreateCircle(string name, Vector2 center, float radius, Color color)
            => Ink.Shape(name, null, center, Vector2.one * Mathf.Max(.01f, radius) * 2, color, -590, true);

        // 게임 진행 중이고 Hunter가 존재할 때만 수명과 노출 시간을 갱신합니다.
        void Update()
        {
            var game = Dungeon.I;
            if (game && game.Running && game.Hero) Advance(game, Time.deltaTime);
        }

        // 남은 수명 안에서 연속 노출을 누적해 주기 피해를 주며, 범위를 벗어나면 노출 시간을 초기화합니다.
        public void Advance(Dungeon game, float dt)
        {
            if (expired) return;
            float activeTime = Mathf.Min(Mathf.Max(0, dt), remaining);
            remaining -= activeTime;
            if (Vector2.Distance(transform.position, game.Pos) <= Radius)
            {
                insideTime += activeTime;
                while (insideTime >= Interval)
                {
                    insideTime -= Interval;
                    game.Hero.HurtOverTime(Damage);
                }
            }
            else insideTime = 0; // 나갔다 들어오면 다시 1초부터 셉니다.
            AdvanceEnemies(game, activeTime);
            if (remaining <= 0)
            {
                expired = true;
                Remove(gameObject);
            }
        }

        void AdvanceEnemies(Dungeon game, float dt)
        {
            // 사망 이벤트가 적 목록을 변경해도 이번 검사 대상은 유지합니다.
            enemySnapshot.Clear();
            enemySnapshot.AddRange(game.Enemies);
            previousTargets.Clear();
            previousTargets.AddRange(enemyExposure.Keys);
            foreach (var enemy in previousTargets)
                if (!enemy || !enemySnapshot.Contains(enemy) || !CanPoison(enemy)) enemyExposure.Remove(enemy);

            foreach (var enemy in enemySnapshot)
            {
                if (!CanPoison(enemy)) continue;
                enemyExposure.TryGetValue(enemy, out float elapsed);
                elapsed += dt;
                while (elapsed >= Interval && enemy && !enemy.Dead && enemy.gameObject.activeInHierarchy)
                {
                    elapsed -= Interval;
                    enemy.TakeEnvironmentDamage(Damage);
                }
                if (enemy && !enemy.Dead) enemyExposure[enemy] = elapsed;
                else enemyExposure.Remove(enemy);
            }
        }

        bool CanPoison(Enemy enemy)
            => enemy && !enemy.Dead && enemy.gameObject.activeInHierarchy &&
               !enemy.GetComponent<RobeManAttack>() &&
               Vector2.Distance(transform.position, enemy.transform.position) <= Radius;

        // 실행 중에는 프레임 종료 시, 에디터 검사 중에는 즉시 오브젝트를 제거합니다.
        public static void Remove(GameObject target)
        {
            if (!target) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
