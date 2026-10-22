using UnityEngine;

namespace WesternLemegeton
{
    // 체력 계산만 담당합니다. 보상과 사망 연출은 Enemy가 처리합니다.
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour
    {
        [Min(1)] public float BaseHealth = 55;
        [Min(0)] public float HealthPerRoom = 13;
        public float Current { get; set; }
        public float Maximum { get; set; }
        public bool Dead => Current <= 0;

        public void Initialize(int room)
        {
            Maximum = Mathf.Max(1, BaseHealth + Mathf.Max(0, room) * HealthPerRoom);
            Current = Maximum;
        }

        public void ApplyDamage(float damage)
        {
            Current = Mathf.Max(0, Current - Mathf.Max(0, damage));
        }
    }
}
