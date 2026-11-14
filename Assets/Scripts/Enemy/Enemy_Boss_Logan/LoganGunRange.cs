using UnityEngine;

namespace WesternLemegeton
{
    // Logan 전용 표시. 탄환과 같은 총구·3D 방향·비행 거리로 경계를 계산합니다.
    public sealed partial class LoganBossAttack
    {
        public bool ShowGunRanges = true;
        [Min(.01f)] public float GunRangeLineWidth = .06f;
        public Color GunRangeColor = new Color(1, .65f, .1f, .7f);
        LineRenderer gunRange;
        Material gunRangeMaterial;

        void UpdateGunRange()
        {
            bool shotgun = ActivePattern == Pattern.Shotgun;
            bool visible = ShowGunRanges && owner && !owner.Dead &&
                (ActivePattern == Pattern.MachineGun || ActivePattern == Pattern.Barrage || shotgun) &&
                (Action == ActionState.Aim || Action == ActionState.Fire);
            if (!visible) { if (gunRange) gunRange.enabled = false; return; }
            if (!gunRange)
            {
                var root = new GameObject("Logan / Gun range");
                root.transform.SetParent(transform, false);
                gunRange = root.AddComponent<LineRenderer>();
                gunRangeMaterial = new Material(Shader.Find("Sprites/Default"));
                gunRange.sharedMaterial = gunRangeMaterial;
                gunRange.useWorldSpace = true;
                gunRange.sortingOrder = 10000;
            }
            gunRange.enabled = true;
            gunRange.startWidth = gunRange.endWidth = Mathf.Max(.01f, GunRangeLineWidth);
            gunRange.startColor = gunRange.endColor = GunRangeColor;
            Vector3 origin = CombatBody.For(owner).Muzzle;
            Vector3 target = Action == ActionState.Aim && Dungeon.I && Dungeon.I.Hero
                ? CombatBody.For(Dungeon.I.Hero).Center : LockedAim;
            Vector3 direction = GunDirection(origin, target);
            float spread = shotgun ? (Shotgun.PelletCount > 1 ? Shotgun.SpreadAngle : 0) :
                ActivePattern == Pattern.Barrage ? Barrage.SweepAngle : 0;
            float distance = shotgun ? Mathf.Max(.01f, Shotgun.BulletDistance) :
                Mathf.Max(0, Settings.BulletSpeed) * Mathf.Max(0, Settings.BulletLifetime);
            if (spread <= 0)
            {
                gunRange.positionCount = 2;
                gunRange.SetPosition(0, origin);
                gunRange.SetPosition(1, GunRangeEnd(origin, direction, distance));
                return;
            }
            int segments = Mathf.Max(2, Mathf.CeilToInt(spread / 3));
            gunRange.positionCount = segments + 3;
            gunRange.SetPosition(0, origin);
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-spread * .5f, spread * .5f, i / (float)segments);
                Vector3 ray = Quaternion.AngleAxis(-angle, Vector3.up) * direction;
                gunRange.SetPosition(i + 1, GunRangeEnd(origin, ray, distance));
            }
            gunRange.SetPosition(segments + 2, origin);
        }
        Vector3 GunRangeEnd(Vector3 origin, Vector3 direction, float distance)
            => origin + direction * ProjectileSpace.WallDistance(new Ray(origin, direction), distance, .125f);

        // 헌터와의 높이 차이로 근접 사격이 바닥에 일찍 닿지 않도록 수평 방향만 사용합니다.
        // 범위 표시와 실제 탄환이 같은 방향 계산을 공유합니다(Logan 전용).
        static Vector3 GunDirection(Vector3 origin, Vector3 target)
        {
            Vector3 direction = target - origin;
            direction.y = 0;
            return direction.sqrMagnitude > .000001f ? direction.normalized : Vector3.right;
        }
        void FireGunBullet(Vector3 target, float speed, float angle, float damage, float life, int crowdControl)
        {
            Vector3 origin = CombatBody.For(owner).Muzzle;
            Vector3 direction = Quaternion.AngleAxis(-angle, Vector3.up) * GunDirection(origin, target);
            Bullet.SpawnWorld(origin, direction * speed, damage, true, life, crowdControl: crowdControl);
        }
    }
}
