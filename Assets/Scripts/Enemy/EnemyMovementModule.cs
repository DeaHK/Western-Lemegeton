using UnityEngine;

namespace WesternLemegeton
{
    // 한 유닛에는 이동 부품 하나를 사용합니다. 목표 위치는 Brain이 전달합니다.
    [DisallowMultipleComponent]
    public abstract class EnemyMovementModule : MonoBehaviour
    {
        protected EnemyAttackModule Attack { get; private set; }

        public virtual void Initialize(EnemyAttackModule attack) => Attack = attack;

        public abstract void Tick(Enemy owner, Dungeon game, Vector2 target, float speedMultiplier, float dt);
    }
}
