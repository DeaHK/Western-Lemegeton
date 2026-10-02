using UnityEngine;

namespace WesternLemegeton
{
    public class Player : MonoBehaviour
    {
        #region Fields and state by responsibility

        // Health / Damage / Invulnerability
        public const float MaxHp = 100;
        public float Hp = 100;
        float invulnerable;
        public bool Invulnerable => invulnerable > 0;
        public int DamageEvents { get; private set; }
        public float DamageScale => 1 + Dungeon.I.Build.PlayerDamageBonus + (Fury > 0 ? .18f + .12f * Dungeon.I.FuryLevel : 0);

        // Weapon / Ammo / Reload
        public int Weapon, Ammo = 6;
        public float TagCd, Reload, Fury;

        // Skills (dashHits tracks Slash hits, not the dodge action)
        readonly float[,] cds = new float[2, 2];
        int skill, skillWeapon, skillSlot, ticks;
        float elapsed;
        Vector2 skillDirection;
        readonly System.Collections.Generic.HashSet<Enemy> dashHits = new System.Collections.Generic.HashSet<Enemy>();
        public float Skill2Cd { get => cds[Weapon, 0]; set => cds[Weapon, 0] = value; }
        public float Skill3Cd { get => cds[Weapon, 1]; set => cds[Weapon, 1] = value; }
        public bool ActionLocked => skill == 2;
        public bool SkillBusy => skill != 0;
        public int ActiveSkill => skill;
        public float SkillElapsed => elapsed;

        // Basic Attack (actionId is also used by skills)
        float attackCd, buffer, basicTime = 10, basicDuration;
        int basicWeapon, basicStage, basicCost;
        long actionId, basicId;
        Vector2 basicDirection;
        bool basicPending;
        public int BasicStage => basicStage;
        public bool BasicBusy => basicTime < basicDuration;

        // Combo / Raven follow-up deduplication
        public int Combo, HitCombo;
        float comboTime, hitTime;
        long lastComboAttack = -1;
        readonly ComboTracker tracker = new ComboTracker();

        // Movement
        public Vector2 Facing = Vector2.right, LastMove = Vector2.right;
        public readonly HunterLocomotion Locomotion = new HunterLocomotion();
        public bool WalkHeld { get; set; }

        // Dash
        public float DashCd;
        float dashTime;
        Vector2 dashDirection;
        public bool Dodging => dashTime > 0;

        // Animation / Presentation requests
        [SerializeField] SpriteRenderer art;
        public SpriteRenderer Presentation => art;
        public long PresentationSequence { get; private set; }
        public int MotionRow { get; private set; }
        public int MotionFrame { get; private set; }
        public MotionPhase Phase { get; private set; }
        public Vector2 MotionDirection => skill != 0 ? skillDirection : basicTime < basicDuration ? basicDirection : Facing;

        #endregion

        #region Input

        private void Update()
        {
            var g = Dungeon.I;
            if (!g.Running) return;
            float dt = Time.deltaTime;
            TickTimers(dt);

            WalkHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            Vector2 move = new Vector2(
                (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)).normalized;
            if (move.sqrMagnitude > 0) LastMove = move;
            Vector2 aim = PaperWorld.Mouse(g.Cam, Input.mousePosition) - (Vector2)transform.position;
            Facing = Weapon == 0 ? LastMove : aim.sqrMagnitude > .1f ? aim.normalized : Facing;

            if (ActionLocked)
            {
                AdvanceActions(dt, Vector2.zero);
                return;
            }

            HandleActionInput(g, move);
            AdvanceActions(dt, move);
        }

        private void HandleActionInput(Dungeon g, Vector2 move)
        {
            if (Input.GetKeyDown(KeyCode.Space)) TryDodge(move);
            if (Input.GetKeyDown(KeyCode.Q)) TryTag();
            HandleRavenInput(g);
            if (Input.GetKeyDown(KeyCode.Alpha2)) TrySkill(0);
            if (Input.GetKeyDown(KeyCode.Alpha3)) TrySkill(1);
            if (Input.GetMouseButtonDown(0) && (!UnityEngine.EventSystems.EventSystem.current || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())) buffer = .18f;
            if (buffer > 0 && TryAttack()) buffer = 0;
        }

        #endregion

        #region Movement

        private void AdvanceActions(float dt, Vector2 move)
        {
            move = Vector2.ClampMagnitude(move, 1);
            Vector2 before = transform.position;
            // Capture before ticking actions: locomotion suppression uses the entry state.
            bool performingAction = skill != 0 || basicTime < basicDuration || dashTime > 0;
            TickSkill(dt);
            TickBasic(dt);
            TickMovement(dt, move);
            Locomotion.Advance((Vector2)transform.position - before, dt, performingAction);
            Animate(move);
        }

        private void TickMovement(float dt, Vector2 move)
        {
            if (dashTime > 0)
            {
                TickDash(dt);
            }
            else if (!ActionLocked && !(skill == 1 && elapsed < HunterMotion.SlashEnd))
                transform.position = Rules.ResolveObstacles(transform.position, (Vector2)transform.position + move * (skill == 4 ? 3.1f : WalkHeld ? 2.8f : 5.6f) * (1 + Dungeon.I.Build.MoveSpeedBonus) * dt, .38f);
        }

        #endregion

        #region Dash

        public bool TryDodge(Vector2 direction)
        {
            if (!Dungeon.I.Running || ActionLocked || DashCd > 0 || direction.sqrMagnitude == 0) return false;
            FinishSkill();
            CancelBasic();
            PresentationSequence++;
            dashDirection = direction.normalized;
            dashTime = .19f;
            DashCd = 2.5f;
            invulnerable = 2;
            buffer = 0;
            Ink.Burst(transform.position, Ink.Cyan);
            return true;
        }

        private void TickDash(float dt)
        {
            float travel = Mathf.Min(dashTime, dt) * 15;
            dashTime = Mathf.Max(0, dashTime - dt);
            int steps = Mathf.Max(1, Mathf.CeilToInt(travel / .25f));
            for (int n = 0; n < steps; n++)
            {
                transform.position = Rules.ResolveDash(transform.position, (Vector2)transform.position + dashDirection * travel / steps, .38f);
            }
            if (dashTime <= 0) transform.position = Rules.OutsideCover(transform.position, .38f);
        }

        #endregion

        #region Health / Damage / Invulnerability

        public void Hit(Enemy e, float damage, int stage = 0, long attack = 0, bool isSkill = false)
        {
            if (!Dungeon.I.Running || !e || e.Dead) return;
            var g = Dungeon.I;
            bool chain = stage > 0 && tracker.Hit(e.GetInstanceID(), stage, attack, g.RunTime);
            // Mark before damage; offer links and combo follow-ups after damage.
            g.Crow.Mark(e);
            e.TakeDamage(damage * DamageScale, g.BurnLevel > 0);
            DamageEvents++;
            HitCombo++;
            hitTime = 3;
            ResolveRavenFollowups(g, e, stage, attack, isSkill, chain);
        }

        public void Hurt(float damage)
        {
            if (invulnerable > 0 || !Dungeon.I.Running || Dungeon.I.IsTown) return;
            Hp = Mathf.Max(0, Hp - damage);
            invulnerable = 1;
            Ink.Burst(transform.position, Ink.Red, 12);
            Dungeon.I.Sound();
            if (Hp <= 0) Dungeon.I.BeginDeath();
        }

        #endregion

        #region Basic Attack

        public bool TryAttack()
        {
            var g = Dungeon.I;
            if (!g.Running || g.IsTown || SkillBusy || dashTime > 0 || attackCd > 0) return false;
            int cost = Rules.AmmoCost(Combo);
            if (Weapon == 1 && Ammo < cost)
            {
                if (Reload <= 0) Reload = 2;
                return false;
            }
            basicStage = Combo + 1;
            actionId++;
            basicId = actionId;
            basicWeapon = Weapon;
            basicCost = cost;
            basicDirection = Facing.normalized;
            basicDuration = attackCd = Weapon == 0 ? .25f : .3f;
            comboTime = Weapon == 0 ? 1.2f : 4;
            basicTime = 0;
            basicPending = true;
            Combo = (Combo + 1) % 4;
            PresentationSequence++;
            return true;
        }

        private void TickBasic(float dt)
        {
            basicTime += dt;
            if (!basicPending || basicTime < basicDuration * .26f) return;
            basicPending = false;
            bool finisher = basicStage == 4;
            var g = Dungeon.I;
            if (basicWeapon == 0)
            {
                ApplyMeleeBasic(g, finisher);
            }
            else
            {
                FireBasicShots(finisher);
            }
            g.Sound(basicWeapon == 1);
        }

        private void ApplyMeleeBasic(Dungeon g, bool finisher)
        {
            foreach (var e in g.Enemies)
                if (e && !e.Dead && Rules.InCone(transform.position, basicDirection, e.transform.position, 2.25f, -.1f))
                    Hit(e, finisher ? 30 : 17, basicStage, basicId);
            SkillEffects.Slash(transform.position, basicDirection, 2.0f);
        }

        private void CancelBasic()
        {
            basicPending = false;
            basicTime = 10;
        }

        #endregion

        #region Weapon / Ammo / Reload

        public bool TryTag()
        {
            if (!Dungeon.I.Running || ActionLocked || TagCd > 0) return false;
            FinishSkill();
            CancelBasic();
            Weapon = 1 - Weapon;
            TagCd = .5f;
            Combo = 0;
            attackCd = 0;
            Fury = 2 + Dungeon.I.FuryLevel;
            Ink.Ring(transform.position, 1, Ink.Gold);
            return true;
        }

        private void FireBasicShots(bool finisher)
        {
            Ammo -= basicCost;
            if (Ammo <= 3 && Reload <= 0) Reload = 2;
            for (int i = 0; i < basicCost; i++)
            {
                Vector2 dir = Quaternion.Euler(0, 0, (i - (basicCost - 1) * .5f) * 7) * basicDirection;
                Bullet.Spawn((Vector2)transform.position + basicDirection * .65f, dir * 21, finisher ? 24 : 20, false, .23f, basicStage, basicId);
            }
            SkillEffects.Shot(transform.position, basicDirection, 1.1f);
        }

        private void TickReload(float dt)
        {
            if (Reload > 0)
            {
                Reload -= dt;
                if (Reload <= 0) Ammo = 6;
            }
        }

        #endregion

        #region Skills

        public bool TrySkill(int slot)
        {
            if (!Dungeon.I.Running || Dungeon.I.IsTown || SkillBusy || basicPending || slot < 0 || slot > 1 || cds[Weapon, slot] > 0) return false;
            CancelBasic();
            skillWeapon = Weapon;
            skillSlot = slot;
            elapsed = 0;
            ticks = 0;
            actionId++;
            buffer = 0;
            skillDirection = (Weapon == 1 ? Facing : LastMove).normalized;
            if (skillDirection.sqrMagnitude < .1f) skillDirection = Vector2.right;
            dashTime = 0;
            skill = Weapon == 0 ? slot + 1 : slot + 3;
            PresentationSequence++;
            if (skill == 1)
            {
                dashHits.Clear();
                invulnerable = .35f;
            }
            if (skill == 2) invulnerable = 2;
            return true;
        }

        private void TickSkill(float dt)
        {
            if (skill == 0) return;
            float previous = elapsed;
            elapsed += dt;
            // Keep independent checks: a completed skill can clear skill within its tick.
            if (skill == 1)
            {
                TickSlash(previous);
            }
            if (skill == 2)
            {
                TickSlam();
            }
            if (skill == 3)
            {
                TickShot();
            }
            if (skill == 4)
            {
                TickBarrage();
            }
        }

        private void TickSlash(float previous)
        {
            if (ticks == 0 && elapsed >= HunterMotion.SlashStart)
            {
                ticks++;
                SkillEffects.Slash(transform.position, skillDirection, 1.65f);
            }
            float active = Mathf.Max(0, Mathf.Min(elapsed, HunterMotion.SlashEnd) - Mathf.Max(previous, HunterMotion.SlashStart));
            if (active > 0)
            {
                float distance = 15 * active;
                int steps = Mathf.Max(1, Mathf.CeilToInt(distance / .20f));
                for (int n = 0; n < steps; n++)
                {
                    transform.position = Rules.ResolveDash(transform.position, (Vector2)transform.position + skillDirection * distance / steps, .38f);
                    foreach (var e in Dungeon.I.Enemies)
                        if (e && !e.Dead && !dashHits.Contains(e) && Vector2.Distance(e.transform.position, transform.position) < 1.6f)
                        {
                            dashHits.Add(e);
                            Hit(e, 36, 0, actionId, true);
                        }
                }
            }
            if (previous < HunterMotion.SlashEnd && elapsed >= HunterMotion.SlashEnd) transform.position = Rules.OutsideCover(transform.position, .38f);
            if (elapsed >= HunterMotion.SlashDuration) FinishSkill();
        }

        private void TickSlam()
        {
            if (ticks == 0 && elapsed >= HunterMotion.SlamHit)
            {
                ticks++;
                Area(2.3f, 20);
                SkillEffects.Impact(transform.position, 2.3f, Ink.Cyan);
            }
            if (ticks == 1 && elapsed >= HunterMotion.SlamEcho)
            {
                ticks++;
                Area(3.2f, 34);
                SkillEffects.Impact(transform.position, 3.2f, Ink.Gold);
            }
            if (elapsed >= HunterMotion.SlamDuration) FinishSkill();
        }

        private void TickShot()
        {
            if (ticks == 0 && elapsed >= HunterMotion.ShotHit)
            {
                ticks++;
                foreach (var e in Dungeon.I.Enemies)
                    if (e && !e.Dead)
                    {
                        Vector2 v = (Vector2)e.transform.position - (Vector2)transform.position;
                        float along = Vector2.Dot(v, skillDirection);
                        if (along >= 0 && along <= 4 && Mathf.Abs(v.x * skillDirection.y - v.y * skillDirection.x) < .8f) Hit(e, 42, 0, actionId, true);
                    }
                SkillEffects.Shot(transform.position, skillDirection, 4);
                Dungeon.I.Sound(true);
            }
            if (elapsed >= HunterMotion.ShotDuration) FinishSkill();
        }

        private void TickBarrage()
        {
            while (ticks < 5 && elapsed >= (ticks + 1) * HunterMotion.BarrageInterval)
            {
                ticks++;
                Area(5, 11);
                SkillEffects.Impact(transform.position, 5, Ink.Gold);
                Dungeon.I.Sound(true);
            }
            if (elapsed >= HunterMotion.BarrageDuration) FinishSkill();
        }

        private void FinishSkill()
        {
            if (skill != 0) cds[skillWeapon, skillSlot] = 8;
            skill = 0;
        }

        #endregion

        #region Combo

        private void ClearExpiredCombos()
        {
            if (comboTime <= 0) Combo = 0;
            if (hitTime <= 0) HitCombo = 0;
        }

        #endregion

        #region Raven integration

        private void HandleRavenInput(Dungeon g)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) g.Crow.Command();
            if (Input.GetKeyDown(KeyCode.R)) g.Crow.TryLink();
        }

        private void ResolveRavenFollowups(Dungeon g, Enemy e, int stage, long attack, bool isSkill, bool chain)
        {
            if (isSkill || stage == 4) g.Crow.OfferLink(e, attack);
            if (chain && lastComboAttack != attack)
            {
                lastComboAttack = attack;
                g.Crow.ComboFollowup(e);
            }
        }

        #endregion

        #region Animation / Presentation requests

        private void Animate(Vector2 move)
        {
            MotionRow = Weapon == 0 ? 0 : 2;
            MotionFrame = 0;
            Phase = MotionPhase.Idle;
            float lift = 0, lean = 0;
            if (skill != 0)
            {
                MotionRow = skill - 1;
                MotionFrame = HunterMotion.Frame(skill, elapsed);
                Phase = HunterMotion.Phase(skill, elapsed);
                lift = HunterMotion.Lift(skill, elapsed);
            }
            else if (basicTime < basicDuration)
            {
                MotionRow = basicWeapon == 0 ? 0 : 2;
                float t = basicTime / basicDuration;
                MotionFrame = t < .07f ? 0 : t < .26f ? 1 : t < .44f ? 2 : t < .61f ? 3 : t < .79f ? 4 : 5;
                Phase = MotionFrame == 0 ? MotionPhase.Idle : MotionFrame == 1 ? MotionPhase.Anticipation : MotionFrame == 2 ? MotionPhase.Action : MotionFrame < 5 ? MotionPhase.Hit : MotionPhase.Recovery;
            }
            else if (dashTime > 0)
            {
                MotionRow = 0;
                MotionFrame = 2;
                lean = -10;
            }
            if (Phase == MotionPhase.Hit && skill == 3) lean = 4;
            bool travelPose = skill == 0 && basicTime >= basicDuration && dashTime <= 0;
            if (travelPose)
            {
                MotionRow = Locomotion.Row;
                MotionFrame = Locomotion.Frame;
                HunterLocomotionAtlas.Pose(art, MotionRow, MotionFrame);
                art.flipX = Locomotion.Mirror;
            }
            else
            {
                HunterAtlas.Pose(art, MotionRow, MotionFrame);
                art.flipX = MotionDirection.x < -.05f;
            }
            art.transform.localRotation = Quaternion.Euler(0, 0, lean * (art.flipX ? -1 : 1));
            art.transform.localPosition = new Vector2(0, -.35f + lift);
            art.color = new Color(1, 1, 1, invulnerable > 0 && Dungeon.I && !Dungeon.I.IsTown ? .7f + .3f * Mathf.Abs(Mathf.Sin(Time.time * 18)) : 1);
        }

        #endregion

        #region Reset / Safe Entry

        private void Awake()
        {
            if (!art) art = CombatArt.Actor(transform, "Hunter", 1.9f);
            Animate(Vector2.zero);
        }

        public void ResetForRun()
        {
            Hp = MaxHp;
            Weapon = Combo = HitCombo = DamageEvents = 0;
            Ammo = 6;
            DashCd = TagCd = Reload = Fury = attackCd = comboTime = invulnerable = elapsed = buffer = hitTime = 0;
            System.Array.Clear(cds, 0, cds.Length);
            LastMove = Facing = Vector2.right;
            WalkHeld = false;
            actionId = basicId = 0;
            lastComboAttack = -1;
            dashHits.Clear();
            SafeEntry();
        }

        public void SafeEntry()
        {
            invulnerable = 1;
            dashTime = 0;
            skill = 0;
            attackCd = buffer = 0;
            basicPending = false;
            basicTime = 10;
            tracker.Clear();
            Locomotion.Reset(LastMove);
            Animate(Vector2.zero);
        }

        #endregion

        #region Utility / Query / Shared timing

        public float Cooldown(int weapon, int slot) => cds[weapon, slot];

        // Shared deterministic stepping also lets the executable checks verify real collision timing.
        internal void AdvanceCombat(float dt, Vector2 move)
        {
            if (!Dungeon.I.Running) return;
            TickTimers(dt);
            AdvanceActions(dt, move);
        }

        private void TickTimers(float dt)
        {
            DashCd = Mathf.Max(0, DashCd - dt);
            TagCd = Mathf.Max(0, TagCd - dt);
            for (int w = 0; w < 2; w++)
                for (int s = 0; s < 2; s++)
                    cds[w, s] = Mathf.Max(0, cds[w, s] - dt);
            Fury -= dt;
            invulnerable -= dt;
            attackCd -= dt;
            comboTime -= dt;
            buffer -= dt;
            hitTime -= dt;
            ClearExpiredCombos();
            TickReload(dt);
        }

        private void Area(float radius, float damage)
        {
            foreach (var e in Dungeon.I.Enemies)
                if (e && !e.Dead && Vector2.Distance(transform.position, e.transform.position) < radius)
                    Hit(e, damage, 0, actionId, true);
        }

        #endregion
    }
}
