using UnityEngine;

namespace WesternLemegeton
{
    public sealed class EnemyKeepDistanceMovement : EnemyMovementModule
    {
        [Min(0)] public float Speed = 2.2f;
        [Tooltip("공격 부품의 현재 사거리에 접근 비율을 적용합니다. 비활성화된 공격도 설정값은 사용합니다.")]
        public bool UseAttackRange = true;
        [Tooltip("공격 부품이 없거나 Use Attack Range를 끈 경우의 기준 거리입니다.")]
        [Min(0)] public float ApproachRange = 8;
        [Min(0)] public float ApproachRangeFactor = .8f;
        [Min(0)] public float RetreatDistance = 4;
        [Min(0)] public float BodyRadius = .5f;
        // 기존 코드의 제곱 거리 1.4를 그대로 사용하여 경계 판정도 유지합니다.
        [Min(0)] public float SeparationDistanceSquared = 1.4f;
        [Min(0)] public float SeparationStrength = .7f;

        public override void Tick(Enemy owner, Dungeon game, Vector2 target, float speedMultiplier, float dt)
        {
            Vector2 position = transform.position;
            Vector2 delta = target - position;
            float distance = delta.magnitude;
            float approachRange = UseAttackRange && Attack ? Attack.AttackRange : ApproachRange;
            // 곱셈 결과를 저장된 거리 상수로 바꾸면 기존 부동소수점 경계 판정이 달라질 수 있습니다.
            Vector2 movement = distance > approachRange * ApproachRangeFactor ? delta.normalized :
                distance < RetreatDistance ? -delta.normalized : Vector2.zero;
            foreach (var other in game.Enemies)
            {
                if (!other || other == owner || other.Dead) continue;
                Vector2 away = position - (Vector2)other.transform.position;
                if (away.sqrMagnitude < SeparationDistanceSquared && away.sqrMagnitude > .01f)
                    movement += away.normalized * SeparationStrength;
            }
            transform.position = Rules.ResolveObstacles(position,
                position + Vector2.ClampMagnitude(movement, 1) * Speed * speedMultiplier * dt, BodyRadius);
        }
    }
}
