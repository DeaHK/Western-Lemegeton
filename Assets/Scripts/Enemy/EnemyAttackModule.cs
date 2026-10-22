using UnityEngine;

namespace WesternLemegeton
{
    [DisallowMultipleComponent]
    public abstract class EnemyAttackModule : MonoBehaviour
    {
        public abstract bool Telegraphing { get; }
        public abstract float AttackPower { get; }
        // 일반 이동이 참조하는 공격 준비 시작 거리입니다. 실제 명중/돌진 거리가 아닙니다.
        public abstract float AttackRange { get; }
        public abstract void Initialize(Enemy owner);
        // true: 이번 프레임의 일반 이동을 막습니다. 돌진 등 공격 자체의 이동은 여기서 처리합니다.
        // false: 공격을 갱신한 후 일반 이동도 실행합니다.
        public abstract bool Tick(Dungeon game, float speedMultiplier, float dt);
        public abstract void Cancel();
    }
}
