using UnityEngine;

namespace WesternLemegeton
{
    public sealed class CowboyAttack : EnemyAttackModule, ISlowMovementSkill
    {
        public enum ActionState { Shoot, ShotWindup, Spin, LassoOut, LassoReturn }
        public ActionState Action { get; private set; }
        public Enemy Owner { get; private set; }
        public bool Slowed => Action == ActionState.Spin || Action == ActionState.LassoOut || Action == ActionState.LassoReturn;
        // 올가미 준비·비행·회수에만 감속을 요청하며, 실제 이동 배율 적용은 공통 Brain이 담당합니다.
        public float SkillMovementMultiplier => Slowed ? LassoMoveMultiplier : 1;
        // 기본 사격의 거리, 준비 시간, 재사용 대기와 탄환 성능입니다.
        [Header("원거리 사격")]
        [Min(0)] public float ShootRange = 8;
        [Min(0)] public float ShotWindup = .6f;
        [Min(.05f)] public float ShotCooldown = 1.5f;
        [Min(0)] public float ShotDamage = 13;
        [Min(.1f)] public float BulletSpeed = 7;
        [Min(0)] public float ProjectileLifetime = 4;
        [Min(1)] public int ProjectileCount = 1;
        public float SpreadAngle = 16;
        [Min(0)] public float WarningRadius = .8f;
        public Vector2 InitialCooldownRange = Vector2.zero;
        // 올가미 준비 연출과 비행·끌림 설정입니다. 머리 위 고리는 공격 판정과 별개입니다.
        [Header("올가미")]
        [Min(0)] public float SpinSeconds = 3;
        [Header("올가미 준비 표시 (공격 판정과 별개)")]
        [Min(0)] public float SpinHeight = 1.8f;
        [Min(0)] public float SpinOrbitRadius = .35f;
        [Min(.01f)] public float SpinDiameter = .45f;
        [Min(0)] public float SpinTurnsPerSecond = 2;
        [Min(.1f)] public float LassoRange = 12;
        [Min(.1f)] public float LassoSpeed = 10;
        [Min(.01f)] public float LassoHitRadius = .55f;
        [Min(.1f)] public float PullSpeed = 1.2f;
        [Range(0, 1)] public float LassoMoveMultiplier = .35f;
        public CowboyRope Rope { get; internal set; }
        float timer, cooldown, distance;
        float spinAngle;
        static Sprite spinRingSprite;
        bool wasInRange;
        Vector2 shotAim, lassoDirection, lassoOrigin, lassoPosition;
        SpriteRenderer spinVisual, lassoVisual;
        public override bool Telegraphing => Action == ActionState.ShotWindup || Action == ActionState.Spin;
        public override float AttackPower => ShotDamage;
        public override float AttackRange => ShootRange;

        // 이전 공격의 표시와 밧줄을 정리하고 새 소유자의 사격 상태로 시작합니다.
        public override void Initialize(Enemy enemy)
        {
            Cancel(); Owner = enemy; Action = ActionState.Shoot; wasInRange = false;
            // 기본 대기 0일 때는 기존처럼 난수를 소비하지 않습니다.
            cooldown = InitialCooldownRange.x == InitialCooldownRange.y ? InitialCooldownRange.x :
                Random.Range(InitialCooldownRange.x, InitialCooldownRange.y);
        }

        // 사격·올가미을 분기합니다. 올가미 중에는 감속 이동을 허용합니다.
        public override bool Tick(Dungeon game, float speedMultiplier, float dt)
        {
            if (!game.Running || !Owner || Owner.Dead) return true;

            cooldown -= dt;
            bool inRange = Vector2.Distance(transform.position, game.Pos) <= ShootRange;
            // 공격 범위 안 -> 밖으로 바뀐 순간에만 올가미를 준비합니다.
            if ((Action == ActionState.Shoot || Action == ActionState.ShotWindup) && wasInRange && !inRange && !Rope)
            {
                Action = ActionState.Spin; timer = SpinSeconds;
                Owner.SetMeleeWarning(false, 1);
                spinAngle = 0;
                spinVisual = Ink.Shape("Cowboy / Overhead lasso", transform, Vector2.zero,
                    Vector2.one * SpinDiameter, Color.yellow, 30, true);
                spinVisual.sprite = GetSpinRingSprite();
                var card = spinVisual.GetComponent<PaperCard>();
                if (card) { card.Flat = true; card.RenderOffset = Vector3.up * SpinHeight; }
            }
            wasInRange = inRange;
            if (Action == ActionState.Spin)
            {
                // 게임 진행 시간으로 회전하므로 일시 정지 중에는 함께 멈춥니다.
                spinAngle = Mathf.Repeat(spinAngle + dt * SpinTurnsPerSecond * Mathf.PI * 2, Mathf.PI * 2);
                if (spinVisual) spinVisual.transform.localPosition = new Vector3(
                    Mathf.Cos(spinAngle), Mathf.Sin(spinAngle), 0) * SpinOrbitRadius;
                timer -= dt;
                if (timer <= 0) ThrowLasso(game);
                return false; // 공통 Brain이 스킬 배율을 적용해 이동 부품을 실행합니다.
            }
            if (Action == ActionState.LassoOut || Action == ActionState.LassoReturn)
            {
                AdvanceLasso(game, dt);
                return false;
            }

            if (Action == ActionState.ShotWindup)
            {
                timer -= dt;
                if (timer <= 0)
                {
                    FireFrom(transform.position, (Vector2)transform.position + shotAim);
                    Owner.SetMeleeWarning(false, 1);
                    cooldown = ShotCooldown; Action = ActionState.Shoot;
                }
                return true;
            }
            if (inRange && cooldown <= 0)
            {
                shotAim = (game.Pos - (Vector2)transform.position).normalized;
                timer = ShotWindup; Action = ActionState.ShotWindup;
                Owner.SetMeleeWarning(true, WarningRadius); return true;
            }
            return false;
        }

        // 가운데가 비어 있는 작은 원을 만들어 별도 이펙트 프리팹 없이 올가미를 표현합니다.
        static Sprite GetSpinRingSprite()
        {
            if (spinRingSprite) return spinRingSprite;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Cowboy lasso ring";
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float radius = new Vector2(x + .5f - size * .5f, y + .5f - size * .5f).magnitude;
                    pixels[y * size + x] = new Color(1, 1, 1,
                        Mathf.Clamp01(31 - radius) * Mathf.Clamp01(radius - 25));
                }
            texture.SetPixels(pixels); texture.Apply();
            spinRingSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
            return spinRingSprite;
        }

        // EnemyRangedAttack과 같은 중앙 대칭 부채꼴 발사 방식입니다. 원본 모듈은 수정하지 않습니다.
        // 사격은 발사 순간의 탄환 수·간격각·피해·속도·수명을 적용합니다.
        public void FireFrom(Vector2 origin, Vector2 target)
        {
            Vector2 direction = (target - origin).normalized;
            if (direction.sqrMagnitude < .001f) direction = Vector2.right;
            int count = Mathf.Max(1, ProjectileCount);
            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) * .5f) * SpreadAngle;
                Bullet.Spawn(origin, Quaternion.Euler(0, 0, angle) * direction * BulletSpeed,
                    ShotDamage, true, ProjectileLifetime);
            }
        }

        // 발사 순간 Hunter를 향해 방향을 고정합니다.
        void ThrowLasso(Dungeon game)
        {
            RemoveVisual(ref spinVisual);
            lassoOrigin = lassoPosition = transform.position;
            Vector2 target = game.Pos;
            lassoDirection = (target - lassoOrigin).normalized;
            if (lassoDirection.sqrMagnitude < .001f) lassoDirection = Vector2.right;
            distance = 0; Action = ActionState.LassoOut;
            lassoVisual = Ink.Shape("Cowboy / Flying lasso", null, lassoPosition, Vector2.one * .6f, Ink.Gold, -580, true);
        }

        // 비행을 작은 이동으로 나눠 충돌을 검사하고 구속·회수로 전환합니다.
        void AdvanceLasso(Dungeon game, float dt)
        {
            if (Action == ActionState.LassoReturn)
            {
                lassoPosition = Vector2.MoveTowards(lassoPosition, transform.position, LassoSpeed * dt);
                if (lassoVisual) lassoVisual.transform.position = lassoPosition;
                if (Vector2.Distance(lassoPosition, transform.position) < .2f) ResumeShooting();
                return;
            }
            float travel = Mathf.Min(LassoSpeed * dt, Mathf.Max(0, LassoRange - distance));
            int steps = Mathf.Max(1, Mathf.CeilToInt(travel / .1f));
            for (int step = 0; step < steps; step++)
            {
                lassoPosition += lassoDirection * (travel / steps); distance += travel / steps;
                if (lassoVisual) lassoVisual.transform.position = lassoPosition;
                if (HauntedBullAttack.Blocked(lassoPosition, .1f)) { Action = ActionState.LassoReturn; return; }
                if (Vector2.Distance(lassoPosition, game.Pos) <= LassoHitRadius)
                {
                    RemoveVisual(ref lassoVisual);
                    Rope = CowboyRope.Bind(this, game.Hero, PullSpeed, LassoRange * 1.5f);
                    ResumeShooting(); return;
                }

            }
            if (distance >= LassoRange - .0001f) Action = ActionState.LassoReturn;
        }

        // 올가미 표시를 정리해 사격으로 복귀합니다.
        void ResumeShooting()
        {
            RemoveVisual(ref spinVisual); RemoveVisual(ref lassoVisual);
            Action = ActionState.Shoot; cooldown = 0;
        }
        public override void Cancel()
        {
            RemoveVisual(ref spinVisual); RemoveVisual(ref lassoVisual);
            if (Rope) Rope.Release();
            if (Owner) Owner.SetMeleeWarning(false, 1);
            Action = ActionState.Shoot;
            wasInRange = false;
        }
        static void RemoveVisual(ref SpriteRenderer visual)
        {
            if (visual) CowboyRope.RemoveObject(visual.gameObject);
            visual = null;
        }
        void OnDisable() => Cancel();
    }
}


