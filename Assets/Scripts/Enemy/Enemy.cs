using UnityEngine;

namespace WesternLemegeton
{
    using UnityEngine.Serialization;
    using Stigmas;
    public class Enemy : MonoBehaviour
    {
        #region Enemy Fields / Runtime State

        // Identity / health
        public int Kind;
        [HideInInspector] public bool AuxiliaryTarget;
        public event System.Action<Enemy> Damaged;
        public event System.Action<Enemy> Dying;
        // 기존 프리팹의 저장값과 외부 코드의 Hp/MaxHp 접근을 유지합니다.
        [FormerlySerializedAs("Hp"), SerializeField, HideInInspector] float legacyHp;
        [FormerlySerializedAs("MaxHp"), SerializeField, HideInInspector] float legacyMaxHp;
        EnemyBasicBrain basicBrain;
        EnemyHealth health;
        public float Hp
        {
            get => health ? health.Current : legacyHp;
            set { if (health) health.Current = value; else legacyHp = value; }
        }
        public float MaxHp
        {
            get => health ? health.Maximum : legacyMaxHp;
            set { if (health) health.Maximum = value; else legacyMaxHp = value; }
        }
        public readonly StigmaEnemyState Stigma = new StigmaEnemyState();
        readonly StigmaTraceView stigmaView = new StigmaTraceView();
        StigmaOwner lastDamageOwner = StigmaOwner.Player;
        bool slowedAtDeath;

        // Common attack timing / aim
        float cooldown, windup = -1;
        Vector2 aim;
        Vector3 projectileAim;

        // Charge (shared by Kind 1 and Kind 3)
        float chargeTime;

        // Hit flash
        float flash;

        // Existing Kind 3 attack cycle
        int cycle;

        #endregion

        #region Authored References / Presentation References

        public Transform AuthoredArt, HealthFill;
        public SpriteRenderer AuthoredSprite, AuthoredWarning;

        Transform art, bar;
        SpriteRenderer warning;
        public Transform PresentationRoot => art;
        public SpriteRenderer Presentation { get; private set; }

        #endregion

        #region Initialization

        public void Init(int kind, int room)
        {
            Kind = kind;
            basicBrain = GetComponent<EnemyBasicBrain>();
            // 이전 근접형 프리팹/런타임 생성 코드도 새 부품 구조로 연결합니다.
            if (!basicBrain && kind == 0)
            {
                if (!GetComponent<EnemyMovementModule>()) gameObject.AddComponent<EnemyChaseMovement>();
                if (!GetComponent<EnemyAttackModule>()) gameObject.AddComponent<EnemyMeleeAttack>();
                basicBrain = gameObject.AddComponent<EnemyBasicBrain>();
            }
            health = null;
            if (basicBrain)
            {
                basicBrain.Initialize(this, room);
                health = basicBrain.Health;
            }
            else
            {
                MaxHp = kind == 3 ? 650 : kind == 1 ? 80 + room * 13 : 55 + room * 13;
                Hp = MaxHp;
            }
            Color color = kind == 0 ? new Color(.55f, .19f, .23f) : kind == 1 ? new Color(.42f, .32f, .52f) : new Color(.21f, .42f, .4f);
            if (AuthoredArt && AuthoredSprite && HealthFill && AuthoredWarning)
            {
                BindAuthoredPresentation();
                if (!basicBrain || kind == 0) cooldown = Random.Range(.8f, 2);
                return;
            }
            CreateFallbackPresentation(kind, color);
        }

        #endregion

        #region Update Orchestration

        private void Update()
        {
            if (AuxiliaryTarget) return;
            var g = Dungeon.I;
            if (!g.Running || Dead) return;
            float dt = Time.deltaTime;
            Stigma.Tick(dt);
            TickFlash(dt);
            UpdateHealthBar();
            // 조립된 몬스터는 Kind 분기 대신 부품들이 행동을 결정합니다.
            if (basicBrain)
            {
                basicBrain.Tick(g, StigmaMoveMultiplier, dt);
                return;
            }
            TickLegacyBehavior(g, dt);
        }

        // 보스와 이전 생성 코드의 행동 경로. 조립형과의 회귀 검사에서도 동일한 경로를 사용합니다.
        private void TickLegacyBehavior(Dungeon g, float dt)
        {
            Vector2 p = transform.position, delta = GetTargetDelta(g, p);
            float dist = delta.magnitude;
            if (chargeTime > 0)
            {
                TickCharge(g, p, dist, dt);
                return;
            }
            if (windup >= 0)
            {
                TickWindup(dt);
                return;
            }
            cooldown -= dt;
            float range = GetAttackRange();
            if (cooldown <= 0 && dist < range)
            {
                BeginAttackWindup(p, delta);
            }
            else
            {
                TickMovement(g, p, delta, dist, range, dt);
            }
        }

        void LateUpdate() { if (!AuxiliaryTarget) stigmaView.Show(this); }

        #endregion

        #region Target Acquisition

        private Vector2 GetTargetDelta(Dungeon g, Vector2 p) => g.Pos - p;

        #endregion

        #region Movement / Separation / Obstacle Resolution

        private void TickMovement(Dungeon g, Vector2 p, Vector2 delta, float dist, float range, float dt)
        {
            Vector2 movement = dist > range * .8f ? delta.normalized : Kind == 2 && dist < 4 ? -delta.normalized : Vector2.zero;
            foreach (var other in g.Enemies)
                if (other && other != this && !other.Dead)
                {
                    Vector2 away = p - (Vector2)other.transform.position;
                    if (away.sqrMagnitude < 1.4f && away.sqrMagnitude > .01f) movement += away.normalized * .7f;
                }
            transform.position = Rules.ResolveObstacles(p, p + Vector2.ClampMagnitude(movement, 1) * (Kind == 3 ? 1.7f : 2.2f) * StigmaMoveMultiplier * dt, .5f);
        }

        private void TickCharge(Dungeon g, Vector2 p, float dist, float dt)
        {
            chargeTime -= dt;
            transform.position = Rules.ResolveObstacles(p, p + aim * 12 * StigmaMoveMultiplier * dt, .5f);
            // dist is captured before movement, as in the original charge branch.
            if (dist < 1.15f) g.Hero.Hurt(22);
        }

        #endregion

        #region Common Attack Timing

        private float GetAttackRange() => Kind == 0 ? 1.7f : Kind == 1 ? 6 : Kind == 2 ? 8 : 8.5f;

        private void BeginAttackWindup(Vector2 p, Vector2 delta)
        {
            aim = delta.normalized;
            if (Dungeon.I && Dungeon.I.Hero) projectileAim = CombatBody.For(Dungeon.I.Hero).Center;
            windup = Kind == 0 ? .65f : Kind == 1 ? .85f : .9f;
            ShowAttackWarning(p);
        }

        private void TickWindup(float dt)
        {
            windup -= dt;
            if (windup <= 0)
            {
                warning.enabled = false;
                Execute();
                windup = -1;
                cooldown = Kind == 3 ? 1.3f : Kind == 1 ? 2 : 1.5f;
            }
        }

        private void Execute()
        {
            Vector2 p = transform.position;
            var g = Dungeon.I;
            if (Kind == 1)
            {
                BeginChargeAttack(p);
            }
            if (Kind == 2) FireRangedProjectiles(p);
            if (Kind == 3)
            {
                ExecuteBossPattern(p);
            }
        }

        #endregion

        #region Kind 1 Charge

        private void BeginChargeAttack(Vector2 p)
        {
            chargeTime = .45f;
            Ink.Line(p, p + aim * 5.4f, Ink.Red, .2f, .35f);
        }

        #endregion

        #region Kind 2 Ranged Projectile

        private void FireRangedProjectiles(Vector2 p)
        {
            for (int i = -1; i <= 1; i++) Bullet.SpawnAimed(this, projectileAim, 6.5f, i * 16, 13, true, 3);
        }

        #endregion

        #region Kind 3 Existing Boss Pattern

        private void ExecuteBossPattern(Vector2 p)
        {
            cycle++;
            int count = Hp < MaxHp * .5f ? 20 : 12;
            if (cycle % 3 == 0)
            {
                BeginBossCharge(p);
            }
            else FireBossRadialPattern(p, count);
        }

        private void BeginBossCharge(Vector2 p)
        {
            chargeTime = .55f;
            Ink.Ring(p, 2.5f, Ink.Red);
        }

        private void FireBossRadialPattern(Vector2 p, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)count * 360 + cycle * 13) * Mathf.Deg2Rad;
                Bullet.Spawn(p, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (Hp < MaxHp * .5f ? 6 : 4.5f), 17, true, 5, source: this);
            }
        }

        private void CreateBossFallbackPresentation()
        {
            art.localScale = Vector3.one * 1.9f;
            Ink.Shape("Cursed crown", art, new Vector2(0, 1.2f), new Vector2(1, .18f), Ink.Red, 15, false, 12);
        }

        #endregion

        #region Damage / Flash

        public void TakeDamage(float damage)
        {
            TakeStigmaDamage(damage, StigmaOwner.Player, Stigma.Slow(Dungeon.I.Stigmas.Database.Rules.MaximumSlowFraction) > 0);
        }

        // 적의 독처럼 헌터나 까마귀의 공격이 아닌 피해입니다. 처치 시 소유자 성흔 효과를 발동하지 않습니다.
        public void TakeEnvironmentDamage(float damage)
        {
            if (damage <= 0 || !gameObject.activeInHierarchy || Dead || !Dungeon.I || !Dungeon.I.Running) return;
            if (AuxiliaryTarget)
            {
                Hp = 0;
                Damaged?.Invoke(this);
                return;
            }
            ApplyDamageImpact(damage);
            Damaged?.Invoke(this);
            if (Dead) Die(false);
        }

        private void ApplyDamageImpact(float damage)
        {
            if (health) health.ApplyDamage(damage);
            else Hp = Mathf.Max(0, Hp - damage);
            flash = .1f;
            Ink.Burst(transform.position, Ink.Cyan, 5);
        }

        public void TakeStigmaDamage(float damage, StigmaOwner owner, bool slowed)
        {
            if (!gameObject.activeInHierarchy || Dead || Dungeon.I.State == RunState.Cinematic) return;
            if (AuxiliaryTarget)
            {
                if (damage > 0) { Hp = 0; Damaged?.Invoke(this); }
                return;
            }
            lastDamageOwner = owner; slowedAtDeath = slowed;
            ApplyDamageImpact(damage);
            if (damage > 0) Damaged?.Invoke(this);
            if (Dead) Die();
        }
        float StigmaMoveMultiplier => 1 - Stigma.Slow(Dungeon.I.Stigmas ? Dungeon.I.Stigmas.Database.Rules.MaximumSlowFraction : .5f);

        private void TickFlash(float dt)
        {
            flash -= dt;
            art.localScale = Vector3.one * (Kind == 3 ? 1.9f : 1) * (flash > 0 ? 1.1f : 1);
        }

        #endregion

        #region Death

        private void Die(bool triggerOwnerEffects = true)
        {
            Dying?.Invoke(this);
            if (AuxiliaryTarget) { Destroy(gameObject); return; }
            Dungeon.I.Kills++;
            if (Dungeon.I.Stigmas)
            {
                var rules = Dungeon.I.Stigmas.Database.Rules;
                Dungeon.I.Wallet.Add(Kind == 3 ? rules.BossCopper : rules.EnemyCopper);
                if (triggerOwnerEffects) Dungeon.I.Stigmas.Combat?.Killed(lastDamageOwner, slowedAtDeath);
            }
            warning.enabled = false;
            Ink.Burst(transform.position, Ink.Red, 14);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (warning) Destroy(warning.gameObject);
        }

        #endregion

        #region Health Bar / Warning / Presentation

        private void BindAuthoredPresentation()
        {
            art = AuthoredArt;
            Presentation = AuthoredSprite;
            bar = HealthFill;
            warning = AuthoredWarning;
            warning.enabled = false;
        }

        private void CreateFallbackPresentation(int kind, Color color)
        {
            art = new GameObject("Enemy art").transform;
            art.SetParent(transform, false);
            var painted = CombatArt.Actor(art, "Enemy", 1.7f);
            Presentation = painted;
            painted.color = Color.Lerp(Color.white, color, .3f);
            if (kind == 3)
            {
                CreateBossFallbackPresentation();
            }
            var back = Ink.Shape("HP background", transform, new Vector2(0, kind == 3 ? 2.5f : 1.35f), new Vector2(kind == 3 ? 2.3f : 1, .09f), new Color(.05f, .03f, .05f), 20);
            bar = Ink.Shape("HP", back.transform, Vector2.zero, Vector2.one, Ink.Red, 21).transform;
            warning = Ink.Shape("Attack warning", null, transform.position, Vector2.one, new Color(.85f, .035f, .08f, .42f), -600, true);
            warning.enabled = false;
            if (!basicBrain || kind == 0) cooldown = Random.Range(.8f, 2);
            AuthoredArt = art;
            AuthoredSprite = painted;
            HealthFill = bar;
            AuthoredWarning = warning;
        }

        private void UpdateHealthBar()
        {
            bar.localScale = new Vector3(Hp / MaxHp, 1, 1);
        }

        private void ShowAttackWarning(Vector2 p)
        {
            warning.enabled = true;
            warning.transform.position = Kind == 0 ? p : Kind == 1 ? p + aim * 2.8f : p;
            warning.transform.localScale = Kind == 0 ? Vector3.one * 3.1f : Kind == 1 ? new Vector3(6, 1, 1) : Vector3.one * (Kind == 3 ? 5 : 2);
            warning.transform.rotation = Quaternion.Euler(0, 0, Kind == 1 ? Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg : 0);
        }

        #endregion

        #region Utility / Query

        // 공격 부품의 요청을 기존 시각 효과에 연결합니다.
        public void SetMeleeWarning(bool visible, float radius)
            => SetAttackWarning(visible, transform.position, Vector3.one * radius * 2, 0);

        public void SetAttackWarning(bool visible, Vector2 position, Vector3 size, float angle)
        {
            if (!warning) return;
            warning.enabled = visible;
            if (!visible) return;
            warning.transform.position = position;
            warning.transform.localScale = size;
            warning.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        public bool Dead => Hp <= 0;
        // Raven uses this for link target priority; charge contact damage remains 22.
        public float AttackPower => basicBrain ? basicBrain.AttackPower : Kind == 3 ? 25 : Kind == 1 ? 22 : 13;
        public bool Telegraphing => basicBrain ? basicBrain.Telegraphing : windup > 0;

        #endregion
    }

    public class Bullet : MonoBehaviour
    {
        // velocity는 기존 검사/도구용 평면 방향입니다. 비행과 충돌은 worldVelocity를 사용합니다.
        Vector2 velocity;
        Vector3 worldPosition, worldVelocity;
        float life, damage;
        bool hostile;
        int stage;
        long attackId;
        SpriteRenderer sprite;
        PaperCard card;
        public Vector3 WorldPosition => worldPosition;
        public Vector3 WorldVelocity => worldVelocity;

        // 원형 탄막은 평면 방향을 유지하되 표시와 판정을 같은 높이에서 처리합니다.
        public static void Spawn(Vector2 p, Vector2 v, float damage, bool hostile, float life,
            int stage = 0, long attackId = 0, Component source = null)
        {
            Vector3 origin = source ? CombatBody.For(source).Muzzle : PaperWorld.Point(p, .95f);
            SpawnWorld(origin, ProjectileSpace.Direction(v), damage, hostile, life, stage, attackId);
        }

        public static Bullet SpawnWorld(Vector3 origin, Vector3 velocity, float damage, bool hostile,
            float life, int stage = 0, long attackId = 0)
        {
            var sr = Ink.Shape(hostile ? "Hostile bullet" : "Revolver bullet", null,
                ProjectileSpace.Ground(origin), hostile ? Vector2.one * .25f : new Vector2(.4f, .1f),
                hostile ? Ink.Red : Ink.Gold, 1000, hostile);
            var bullet = sr.gameObject.AddComponent<Bullet>();
            bullet.sprite = sr; bullet.card = sr.GetComponent<PaperCard>();
            if (bullet.card) bullet.card.Flat = false;
            bullet.worldPosition = origin; bullet.worldVelocity = velocity;
            bullet.velocity = ProjectileSpace.Ground(velocity).normalized * velocity.magnitude;
            bullet.damage = damage; bullet.hostile = hostile; bullet.life = life;
            bullet.stage = stage; bullet.attackId = attackId;
            bullet.Show();
            return bullet;
        }

        public static void SpawnAimed(Component source, Vector3 target, float speed, float angle,
            float damage, bool hostile, float life, int stage = 0, long attackId = 0)
        {
            Vector3 origin = CombatBody.For(source).Muzzle;
            Vector3 direction = target-origin;
            if (direction.sqrMagnitude < .000001f) direction = Vector3.right;
            // XY 각도 증가를 XZ로 옮기면 Y축 회전 부호가 반대입니다.
            direction = Quaternion.AngleAxis(-angle, Vector3.up) * direction.normalized;
            SpawnWorld(origin, direction * speed, damage, hostile, life, stage, attackId);
        }

        void Update()
        {
            var game = Dungeon.I;
            if (!game || !game.Running) return;
            float dt = Mathf.Min(Time.deltaTime, Mathf.Max(0, life));
            Vector3 next = worldPosition + worldVelocity * dt;
            // 프레임 사이 전체 경로를 검사하므로 빠른 탄환도 몸통/얇은 벽을 건너뛰지 않습니다.
            if (ProjectileSpace.Sweep(game, worldPosition, next, hostile, hostile ? .125f : .05f,
                out var impact, out var enemy, out bool hunter))
            {
                worldPosition = impact; Show();
                SkillEffects.WorldImpact(impact, hostile ? Ink.Red : Ink.Gold);
                if (hunter) game.Hero.Hurt(damage);
                else if (enemy) game.Hero.Hit(enemy, damage, stage, attackId);
                Destroy(gameObject); return;
            }
            worldPosition = next; Show();
            life -= Time.deltaTime;
            if (life <= 0) Destroy(gameObject);
        }

        void LateUpdate() => Show();
        void Show()
        {
            transform.position = ProjectileSpace.Ground(worldPosition);
            if (card) card.RenderOffset = Vector3.up * worldPosition.y;
            // 탄환 그림도 화면에 투영된 실제 비행 방향으로 회전합니다.
            var rotation = Quaternion.Euler(PaperWorld.Pitch, 0, 0);
            Vector3 viewDirection = Quaternion.Inverse(rotation) * worldVelocity;
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(viewDirection.y, viewDirection.x) * Mathf.Rad2Deg);
        }
    }
}
