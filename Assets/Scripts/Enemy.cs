using UnityEngine;

namespace WesternLemegeton
{
    public class Enemy : MonoBehaviour
    {
        #region Enemy Fields / Runtime State

        // Identity / health
        public int Kind;
        public float Hp, MaxHp;

        // Common attack timing / aim
        float cooldown, windup = -1;
        Vector2 aim;

        // Charge (shared by Kind 1 and Kind 3)
        float chargeTime;

        // Burn / flash
        float burnTime, burnTick, flash;

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
            MaxHp = kind == 3 ? 650 : kind == 1 ? 80 + room * 13 : 55 + room * 13;
            Hp = MaxHp;
            Color color = kind == 0 ? new Color(.55f, .19f, .23f) : kind == 1 ? new Color(.42f, .32f, .52f) : new Color(.21f, .42f, .4f);
            if (AuthoredArt && AuthoredSprite && HealthFill && AuthoredWarning)
            {
                BindAuthoredPresentation();
                cooldown = Random.Range(.8f, 2);
                return;
            }
            CreateFallbackPresentation(kind, color);
        }

        #endregion

        #region Update Orchestration

        private void Update()
        {
            var g = Dungeon.I;
            if (!g.Running || Dead) return;
            float dt = Time.deltaTime;
            if (burnTime > 0)
            {
                TickBurnTimers(dt);
                if (burnTick <= 0)
                {
                    ApplyBurnTick(g);
                    if (Dead) return;
                }
            }
            TickFlash(dt);
            UpdateHealthBar();
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
            transform.position = Rules.ResolveObstacles(p, p + Vector2.ClampMagnitude(movement, 1) * (Kind == 3 ? 1.7f : 2.2f) * dt, .5f);
        }

        private void TickCharge(Dungeon g, Vector2 p, float dist, float dt)
        {
            chargeTime -= dt;
            transform.position = Rules.ResolveObstacles(p, p + aim * 12 * dt, .5f);
            // dist is captured before movement, as in the original charge branch.
            if (dist < 1.15f) g.Hero.Hurt(22);
        }

        #endregion

        #region Common Attack Timing

        private float GetAttackRange() => Kind == 0 ? 1.7f : Kind == 1 ? 6 : Kind == 2 ? 8 : 8.5f;

        private void BeginAttackWindup(Vector2 p, Vector2 delta)
        {
            aim = delta.normalized;
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
            // Keep the original independent Kind checks.
            if (Kind == 0)
            {
                ExecuteMeleeAttack(p, g);
            }
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

        #region Kind 0 Melee

        private void ExecuteMeleeAttack(Vector2 p, Dungeon g)
        {
            Ink.Ring(p, 1.55f, Ink.Red);
            if (Vector2.Distance(p, g.Pos) < 1.75f) g.Hero.Hurt(15);
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
            for (int i = -1; i <= 1; i++) Bullet.Spawn(p, Quaternion.Euler(0, 0, i * 16) * aim * 6.5f, 13, true, 3);
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
                Bullet.Spawn(p, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (Hp < MaxHp * .5f ? 6 : 4.5f), 17, true, 5);
            }
        }

        private void CreateBossFallbackPresentation()
        {
            art.localScale = Vector3.one * 1.9f;
            Ink.Shape("Cursed crown", art, new Vector2(0, 1.2f), new Vector2(1, .18f), Ink.Red, 15, false, 12);
        }

        #endregion

        #region Damage / Burn / Flash

        public void TakeDamage(float damage, bool ignite)
        {
            if (Dead || Dungeon.I.State == RunState.Cinematic) return;
            ApplyDamageImpact(damage, ignite);
            if (ignite) burnTime = 3;
            if (Dead)
            {
                Die();
            }
        }

        private void ApplyDamageImpact(float damage, bool ignite)
        {
            Hp = Mathf.Max(0, Hp - damage);
            flash = .1f;
            Ink.Burst(transform.position, ignite ? Ink.Gold : Ink.Cyan, 5);
        }

        private void TickBurnTimers(float dt)
        {
            burnTime -= dt;
            burnTick -= dt;
        }

        private void ApplyBurnTick(Dungeon g)
        {
            burnTick = .5f;
            TakeDamage(3 + g.BurnLevel * 2, false);
            Ink.Burst(transform.position, Ink.Gold, 3);
        }

        private void TickFlash(float dt)
        {
            flash -= dt;
            art.localScale = Vector3.one * (Kind == 3 ? 1.9f : 1) * (flash > 0 ? 1.1f : 1);
        }

        #endregion

        #region Death

        private void Die()
        {
            Dungeon.I.Kills++;
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
            cooldown = Random.Range(.8f, 2);
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

        public bool Dead => Hp <= 0;
        // Raven uses this for link target priority; charge contact damage remains 22.
        public float AttackPower => Kind == 3 ? 25 : Kind == 1 ? 22 : Kind == 0 ? 15 : 13;
        public bool Telegraphing => windup > 0;

        #endregion
    }

    public class Bullet : MonoBehaviour
    {
        #region Bullet Fields / Initialization

        // Movement / lifetime
        Vector2 velocity;
        float life;

        // Hit payload / allegiance
        float damage;
        bool hostile;
        int stage;
        long attackId;

        public static void Spawn(Vector2 p, Vector2 v, float damage, bool hostile, float life, int stage = 0, long attackId = 0)
        {
            var sr = CreatePresentation(p, v, hostile);
            ApplyMuzzleOffset(sr, hostile);
            var b = sr.gameObject.AddComponent<Bullet>();
            InitializeBullet(b, v, damage, hostile, life, stage, attackId);
        }

        private static void InitializeBullet(Bullet b, Vector2 v, float damage, bool hostile, float life, int stage, long attackId)
        {
            b.velocity = v;
            b.damage = damage;
            b.hostile = hostile;
            b.life = life;
            b.stage = stage;
            b.attackId = attackId;
        }

        #endregion

        #region Bullet Movement / Collision / Lifetime

        private void Update()
        {
            var g = Dungeon.I;
            if (!g.Running) return;
            float dt = Time.deltaTime;
            life -= dt;
            if (life <= 0)
            {
                Destroy(gameObject);
                return;
            }
            // Substeps avoid tunnelling at low frame rates and into cover.
            int steps = GetSubstepCount(dt);
            for (int i = 0; i < steps; i++)
            {
                MoveSubstep(dt, steps);
                Vector2 p = transform.position;
                if (Mathf.Abs(p.x - Rules.RoomOrigin.x) > 14.8f || Mathf.Abs(p.y - Rules.RoomOrigin.y) > 8.8f)
                {
                    Destroy(gameObject);
                    return;
                }
                foreach (Rect r in Dungeon.Obstacles)
                    if (r.Contains(p))
                    {
                        HitObstacle(p);
                        return;
                    }
                if (hostile)
                {
                    if (Vector2.Distance(p, g.Pos) < .48f)
                    {
                        HitPlayer(g);
                        return;
                    }
                }
                else
                    foreach (var e in g.Enemies)
                        if (e && !e.Dead && Vector2.Distance(p, e.transform.position) < (e.Kind == 3 ? 1 : .55f))
                        {
                            HitEnemy(g, e);
                            return;
                        }
            }
        }

        private int GetSubstepCount(float dt) => Mathf.Max(1, Mathf.CeilToInt(velocity.magnitude * dt / .16f));

        private void MoveSubstep(float dt, int steps)
        {
            transform.position += (Vector3)(velocity * dt / steps);
        }

        private void HitObstacle(Vector2 p)
        {
            Ink.Burst(p, Ink.Gold, 2);
            Destroy(gameObject);
        }

        private void HitPlayer(Dungeon g)
        {
            g.Hero.Hurt(damage);
            Destroy(gameObject);
        }

        private void HitEnemy(Dungeon g, Enemy e)
        {
            g.Hero.Hit(e, damage, stage, attackId);
            Destroy(gameObject);
        }

        #endregion

        #region Bullet Presentation / Utility

        private static SpriteRenderer CreatePresentation(Vector2 p, Vector2 v, bool hostile) => Ink.Shape(hostile ? "Hostile bullet" : "Revolver bullet", null, p, hostile ? Vector2.one * .25f : new Vector2(.4f, .1f), hostile ? Ink.Red : Ink.Gold, 1000, hostile, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);

        private static void ApplyMuzzleOffset(SpriteRenderer sr, bool hostile)
        {
            if (!hostile)
            {
                var card = sr.GetComponent<PaperCard>();
                if (card) card.RenderOffset = SkillEffects.MuzzleOffset;
            }
        }

        #endregion
    }
}
