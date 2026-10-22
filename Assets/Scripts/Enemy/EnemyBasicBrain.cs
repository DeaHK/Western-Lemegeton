using UnityEngine;

namespace WesternLemegeton
{
    // 개별 부품의 실행 순서만 결정합니다. 별도의 Update는 두지 않습니다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyBasicBrain : MonoBehaviour
    {
        Enemy owner;
        EnemyMovementModule movement;
        EnemyAttackModule attack;
        public EnemyHealth Health { get; private set; }
        public bool Telegraphing => isActiveAndEnabled && attack && attack.isActiveAndEnabled && attack.Telegraphing;
        public float AttackPower => attack && attack.isActiveAndEnabled ? attack.AttackPower : 0;

        public void Initialize(Enemy enemy, int room)
        {
            owner = enemy;
            Health = GetComponent<EnemyHealth>();
            movement = GetComponent<EnemyMovementModule>();
            attack = GetComponent<EnemyAttackModule>();
            Health.Initialize(room);
            if (attack) attack.Initialize(enemy);
            if (movement) movement.Initialize(attack);
        }

        public void Tick(Dungeon game, float speedMultiplier, float dt)
        {
            if (!isActiveAndEnabled) return;
            if (attack && attack.isActiveAndEnabled && attack.Tick(game, speedMultiplier, dt)) return;
            if (movement && movement.isActiveAndEnabled)
                movement.Tick(owner, game, game.Pos, speedMultiplier, dt);
        }

        void OnDisable()
        {
            if (attack) attack.Cancel();
        }
    }
}
