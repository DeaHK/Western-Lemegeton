using UnityEngine;

namespace WesternLemegeton
{
    // 한 유닛에는 이동 부품 하나를 사용합니다. 목표 위치는 Brain이 전달합니다.
    [DisallowMultipleComponent]
    public abstract class EnemyMovementModule : MonoBehaviour
    {
        protected EnemyAttackModule Attack { get; private set; }
        [Tooltip("좌우 우회 선택 시 주변 몬스터를 확인하는 거리")]
        [Min(.1f)] public float SideCheckDistance = 2;
        [Tooltip("헌터 방향으로 이 거리 이내의 이동 경로가 막혔을 때만 우회를 시작합니다. 몸 반경은 별도로 반영합니다.")]
        [Min(.01f)] public float FrontObstacleCheckDistance = .3f;
        int detourSide;

        public virtual void Initialize(EnemyAttackModule attack) { Attack = attack; detourSide = 0; }

        // 추적/거리 유지의 일반 이동만 우회합니다. 돌진과 도약은 원래 공격 규칙을 유지합니다.
        protected bool TrySideDetour(Enemy owner, Dungeon game, Vector2 target, float radius, float travel)
        {
            Vector2 position = transform.position;
            if (ClearSegment(position, target, radius)) { detourSide = 0; return false; }
            if (detourSide == 0)
            {
                Vector2 forward = target - position;
                float checkDistance = Mathf.Min(forward.magnitude, Mathf.Max(FrontObstacleCheckDistance, travel));
                // 멀리 있는 장애물만 보이면 정상 접근하고, 바로 앞이 막혔을 때 우회를 시작합니다.
                if (ClearSegment(position, position + forward.normalized * checkDistance, radius)) return false;
            }
            if (travel <= 0) return true;
            Vector2 left = Left(position, target);
            bool leftOccupied = false, rightOccupied = false;
            foreach (var other in game.Enemies)
            {
                if (!other || other == owner || other.Dead || other.AuxiliaryTarget || !other.gameObject.activeInHierarchy) continue;
                Vector2 offset = (Vector2)other.transform.position - position;
                if (offset.sqrMagnitude > SideCheckDistance * SideCheckDistance) continue;
                float side = Vector2.Dot(offset, left);
                if (side > .05f) leftOccupied = true;
                if (side < -.05f) rightOccupied = true;
            }
            if (detourSide == 0)
            {
                float leftDistance = ClearanceDistance(position, target, radius, 1);
                float rightDistance = ClearanceDistance(position, target, radius, -1);
                detourSide = leftOccupied != rightOccupied ? (leftOccupied ? -1 : 1) :
                    leftDistance <= rightDistance ? 1 : -1;
                // 우선 방향이 벽에 막혔으면 실제로 이동 가능한 반대편을 사용합니다.
                if (!CanStep(position, position + left * detourSide * .15f, radius)) detourSide = -detourSide;
            }
            int steps = Mathf.Max(1, Mathf.CeilToInt(travel / .15f));
            for (int i = 0; i < steps; i++)
            {
                if (ClearSegment(position, target, radius)) { detourSide = 0; break; }
                Vector2 next = position + Left(position, target) * detourSide * (travel / steps);
                if (!CanStep(position, next, radius)) { detourSide = 0; break; }
                position = next;
            }
            transform.position = position;
            return true;
        }

        static Vector2 Left(Vector2 position, Vector2 target)
        {
            Vector2 forward = (target - position).normalized;
            return new Vector2(-forward.y, forward.x);
        }
        // 실제 이동과 같은 측면 경로를 양쪽으로 미리 따라가며 직선 접근이 열리는 거리를 비교합니다.
        static float ClearanceDistance(Vector2 position, Vector2 target, float radius, int side)
        {
            const float step = .15f;
            for (int i = 1; i <= 267; i++)
            {
                Vector2 next = position + Left(position, target) * side * step;
                if (!CanStep(position, next, radius)) return float.PositiveInfinity;
                position = next;
                if (ClearSegment(position, target, radius)) return i * step;
            }
            return float.PositiveInfinity;
        }
        static bool CanStep(Vector2 from, Vector2 to, float radius)
            => (Rules.ClampToRoom(to, radius) - to).sqrMagnitude < .000001f && ClearSegment(from, to, radius);

        static bool ClearSegment(Vector2 from, Vector2 to, float radius)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < .000001f) return true;
            var ray = new Ray(ProjectileSpace.Direction(from), ProjectileSpace.Direction(delta.normalized));
            foreach (Rect r in Dungeon.Obstacles)
                if (ProjectileSpace.RayBox(ray, new Vector3(r.xMin-radius, -1, r.yMin-radius),
                    new Vector3(r.xMax+radius, 1, r.yMax+radius), delta.magnitude, out _)) return false;
            return true;
        }

        public abstract void Tick(Enemy owner, Dungeon game, Vector2 target, float speedMultiplier, float dt);
    }
}
