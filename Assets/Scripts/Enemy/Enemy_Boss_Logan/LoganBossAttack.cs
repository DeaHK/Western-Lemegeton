using UnityEngine;
using UnityEngine.Events;

namespace WesternLemegeton
{
    // EnemyBossAttack의 패턴별 설정/사용 토글 구조를 바탕으로 만든 Logan 전용 공격 부품입니다.
    [DefaultExecutionOrder(1000)]
    public sealed partial class LoganBossAttack : EnemyAttackModule, ISlowMovementSkill
    {
        public enum Pattern { None, MachineGun, Barrage, Shotgun, Whip, Dynamite }
        public enum ActionState { Chase, Leap, Aim, Fire, Transition, Windup, Active, Swing, Recovery, Dodge }
        [System.Serializable]
        public class GunSettings
        {
            [Range(0, 2)] public int CrowdControl = 1;
            [Min(0)] public float TriggerRange = 10;
            [Min(0)] public float AimSeconds = 1;
            [Min(0)] public float Damage = 10;
            [Min(.01f)] public float ShotInterval = .15f;
            [Min(.01f)] public float Duration = 2;
            [Min(0)] public float RecoverySeconds = .5f;
            [Min(.01f)] public float BulletSpeed = 10;
            [Min(.01f)] public float BulletLifetime = 3;
        }
        [System.Serializable]
        public sealed class BarrageSettings : GunSettings
        {
            [Min(0)] public float Cooldown = 10;
            [Min(0)] public float LeapDistance = 3;
            [Min(.01f)] public float LeapSeconds = .4f;
            [Min(0)] public float LeapHeight = .7f;
            [Range(0,180)] public float SweepAngle = 35;
            [Tooltip("초당 좌우 왕복 횟수")]
            [Min(0)] public float SweepCyclesPerSecond = 1;
            public BarrageSettings() { Duration = 3; ShotInterval = .1f; }
        }
        public bool EnableMachineGun = true;
        public bool EnableBarrage = true;
        public GunSettings MachineGun = new GunSettings();
        public BarrageSettings Barrage = new BarrageSettings();
        [Tooltip("채찍 시작 거리 및 총 난사 도약/샷건 휘두르기의 근접 경계")]
        [Min(0)] public float WhipRange = 3;
        [Min(.01f)] public float BodyRadius = .5f;
        [Min(.01f)] public float TransitionSeconds = 5;
        [Range(10,60)] public float TransitionFov = 23;
        public UnityEvent PhaseTwoStarted = new UnityEvent();
        public Pattern ActivePattern { get; private set; }
        public ActionState Action { get; private set; }
        public int Phase { get; private set; } = 1;
        public float BarrageCooldownRemaining => barrageCooldown;
        public Vector3 LockedAim { get; private set; }
        Enemy owner;
        float elapsed, nextShot, barrageCooldown, visualHeight;
        Vector2 leapStart, leapEnd;
        Dungeon cinematicGame;
        RunState returnState;
        Vector3 savedCameraPosition;
        float savedFov;
        bool transitioning;
        float recoveryRemaining;
        public float RecoveryRemaining => recoveryRemaining;
        GunSettings Settings => ActivePattern == Pattern.Barrage ? Barrage : MachineGun;
        public override bool Telegraphing => Action == ActionState.Aim || Action == ActionState.Leap || Action == ActionState.Windup || Action == ActionState.Swing;
        public override float AttackPower => Phase == 2 ? Mathf.Max(EnableShotgun ? Shotgun.Damage : 0, EnableWhip ? Whip.Damage : 0, EnableDynamite ? Dynamite.Damage : 0) : Mathf.Max(EnableMachineGun ? MachineGun.Damage : 0, EnableBarrage ? Barrage.Damage : 0);
        public override float AttackRange => Phase == 2 ? Shotgun.TriggerRange : Mathf.Max(EnableMachineGun ? MachineGun.TriggerRange : 0, EnableBarrage ? Barrage.TriggerRange : 0);

        [Min(0)] public float PhaseTwoHealth = 350;


        public override void Initialize(Enemy enemy)
        {
            if (owner) owner.Damaged -= OnDamaged;
            Cancel(); owner = enemy;
            Phase = 1; barrageCooldown = 0;
            whipCooldown = dynamiteCooldown = 0;
            owner.Damaged += OnDamaged;
        }
        bool BarrageReady(Dungeon game) => EnableBarrage && barrageCooldown <= 0 &&
            Vector2.Distance(transform.position, game.Pos) <= Barrage.TriggerRange;

        // 외부 애니메이션/패턴 제어에서도 같은 신호 진입점을 사용할 수 있습니다.
        public bool SignalMachineGun() => Request(Pattern.MachineGun);
        public bool SignalBarrage() => Request(Pattern.Barrage);
        bool Request(Pattern pattern)
        {
            var game = Dungeon.I;
            if (!owner || owner.Dead || !game || !game.Running || Phase != 1 || transitioning) return false;
            if (dodging) return false;
            if (pattern == Pattern.Barrage && !BarrageReady(game)) return false;
            if (pattern == Pattern.MachineGun && (!EnableMachineGun || BarrageReady(game) ||
                Vector2.Distance(transform.position, game.Pos) > MachineGun.TriggerRange)) return false;
            if (ActivePattern != Pattern.None)
            {
                // 발사 전 평타만 취소할 수 있습니다. 평타 발사 중/스킬 실행 중에는 교체하지 않습니다.
                if (pattern != Pattern.Barrage || ActivePattern != Pattern.MachineGun || Action != ActionState.Aim) return false;
                CancelAttack();
            }
            ActivePattern = pattern; elapsed = nextShot = 0;
            Action = ActionState.Aim;
            if (pattern == Pattern.Barrage && Vector2.Distance(transform.position, game.Pos) <= WhipRange)
            {
                leapStart = transform.position; leapEnd = FindLeapTarget(game.Pos);
                if (Vector2.Distance(leapStart, leapEnd) > .01f) Action = ActionState.Leap;
            }
            return true;
        }
        public override bool Tick(Dungeon game, float speedMultiplier, float dt)
        {
            if (!game.Running || !owner || owner.Dead) return true;
            if (Phase == 1 && owner.Hp <= PhaseTwoHealth) { StartTransition(game); return true; }
            if (TickDynamiteDodge(game, Mathf.Max(0,dt))) return true;
            if (Action == ActionState.Recovery)
            {
                dt = Mathf.Max(0,dt);
                barrageCooldown = Mathf.Max(0,barrageCooldown-dt);
                whipCooldown = Mathf.Max(0,whipCooldown-dt);
                dynamiteCooldown = Mathf.Max(0,dynamiteCooldown-dt);
                bool enabled = ActivePattern == Pattern.MachineGun ? EnableMachineGun :
                    ActivePattern == Pattern.Barrage ? EnableBarrage : EnabledPhaseTwo(ActivePattern);
                recoveryRemaining = Mathf.Max(0,recoveryRemaining-dt);
                if (!enabled || recoveryRemaining <= 0) CancelAttack(false);
                return true;
            }
            if (Phase == 2) return TickPhaseTwo(game, speedMultiplier, Mathf.Max(0, dt));
            dt = Mathf.Max(0, dt);
            barrageCooldown = Mathf.Max(0, barrageCooldown - dt);
            if ((ActivePattern == Pattern.MachineGun && !EnableMachineGun) || (ActivePattern == Pattern.Barrage && !EnableBarrage)) CancelAttack();
            if (ActivePattern == Pattern.MachineGun && Action == ActionState.Aim && BarrageReady(game) && SignalBarrage())
                return Action == ActionState.Leap;
            if (ActivePattern == Pattern.None)
            {
                if (BarrageReady(game)) SignalBarrage(); else SignalMachineGun();
                // 조준은 이동과 병행하고 도약/실제 사격만 일반 이동을 막습니다.
                return Action == ActionState.Leap || Action == ActionState.Fire;
            }
            if (Action == ActionState.Leap)
            {
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, Barrage.LeapSeconds));
                Vector2 next = Vector2.Lerp(leapStart, leapEnd, t);
                Vector2 from = transform.position;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from,next)/.1f));
                for(int i=1;i<=steps;i++)
                {
                    Vector2 p=Vector2.Lerp(from,next,i/(float)steps);
                    if(HauntedBullAttack.Blocked(p,BodyRadius)) break;
                    transform.position=p;
                }
                SetHeight(4 * Barrage.LeapHeight * t * (1-t));
                if (t >= 1) { SetHeight(0); elapsed = 0; Action = ActionState.Aim; }
                return true;
            }
            if (Action == ActionState.Aim)
            {
                LockedAim = CombatBody.For(game.Hero).Center;
                elapsed += dt;
                owner.SetMeleeWarning(false, 0);
                if (elapsed >= Settings.AimSeconds)
                { Action = ActionState.Fire; elapsed = nextShot = 0; owner.SetMeleeWarning(false, 0); }
                return Action == ActionState.Fire;
            }
            if (Action == ActionState.Fire)
            {
                elapsed += dt;
                float duration = Mathf.Max(.01f, Settings.Duration);
                // 각 탄환의 예정 시각으로 각도를 계산해 프레임 속도에 관계없이 동일한 궤적을 만듭니다.
                while (nextShot < duration && nextShot <= elapsed)
                {
                    float angle = ActivePattern == Pattern.Barrage ?
                        Mathf.Sin(nextShot * Mathf.PI * 2 * Barrage.SweepCyclesPerSecond) * Barrage.SweepAngle * .5f : 0;
                    FireGunBullet(LockedAim, Settings.BulletSpeed, angle, Settings.Damage, Settings.BulletLifetime, Settings.CrowdControl);
                    nextShot += Mathf.Max(.01f, Settings.ShotInterval);
                }
                if (elapsed >= duration) BeginRecovery();
                return true;
            }
            return false;
        }
        public Vector2 FindLeapTarget(Vector2 hunter)
        {
            Vector2 start = transform.position, away = (start-hunter).normalized;
            if (away.sqrMagnitude < .001f) away = Vector2.left;
            Vector2 preferred = start + away * Mathf.Max(0, Barrage.LeapDistance);
            if (ClearPath(start, preferred)) return preferred;
            Vector2 best = start; float bestDistance = (start-hunter).sqrMagnitude;
            // 5도 간격의 방향과 0.1 단위 거리 후보 중 경로 전체가 비어 있는 최원점을 고릅니다.
            for (int a=0; a<72; a++)
            {
                float angle = a * Mathf.PI / 36;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                int steps = Mathf.Max(1, Mathf.CeilToInt(Barrage.LeapDistance / .1f));
                for (int i=1; i<=steps; i++)
                {
                    Vector2 p = start + direction * (Barrage.LeapDistance*i/steps);
                    if (HauntedBullAttack.Blocked(p, BodyRadius)) break;
                    float distance = (p-hunter).sqrMagnitude;
                    if (distance > bestDistance) { bestDistance=distance; best=p; }
                }
            }
            return best;
        }
        bool ClearPath(Vector2 start, Vector2 end)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start,end)/.1f));
            for (int i=1;i<=steps;i++) if (HauntedBullAttack.Blocked(Vector2.Lerp(start,end,i/(float)steps),BodyRadius)) return false;
            return true;
        }
        void SetHeight(float height)
        {
            Vector3 delta = Vector3.up * (height-visualHeight);
            if (owner && owner.Presentation)
            { var card=owner.Presentation.GetComponent<PaperCard>(); if(card) card.RenderOffset+=delta; }
            var presentation=GetComponent<ActorPresentation>();
            if(presentation) presentation.VisualOffset+=delta;
            visualHeight=height;
        }
        void OnDamaged(Enemy enemy)
        {
            var game=Dungeon.I;
            if(game && game.Running && !enemy.Dead && Phase==1 && enemy.Hp<=PhaseTwoHealth) StartTransition(game);
        }
        void StartTransition(Dungeon game)
        {
            if(transitioning) return;
            CancelAttack(); transitioning=true; Action=ActionState.Transition; elapsed=0;
            cinematicGame=game; returnState=game.State;
            savedCameraPosition=game.Cam.transform.position; savedFov=game.Cam.fieldOfView;
            game.State=RunState.Cinematic;
        }
        void Update()
        {
            if(!transitioning) return;
            if(!cinematicGame || cinematicGame.State!=RunState.Cinematic || !owner || owner.Dead) { EndTransition(false); return; }
            elapsed+=Time.unscaledDeltaTime;
            if(elapsed>=TransitionSeconds || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)) SkipTransition();
        }
        void LateUpdate()
        {
            UpdateGunRange();
            if(!transitioning || !cinematicGame || !cinematicGame.Cam) return;
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.5f));
            cinematicGame.Cam.transform.position=Vector3.Lerp(savedCameraPosition,PaperWorld.CameraPosition(transform.position),t);
            cinematicGame.Cam.fieldOfView=Mathf.Lerp(savedFov,TransitionFov,t);
        }
        void OnGUI()
        {
            if(!transitioning) return;
            GUI.Box(new Rect(Screen.width*.5f-150,20,300,40),"LOGAN — PHASE 2");
            if(GUI.Button(new Rect(Screen.width-220,Screen.height-65,200,45),"스킵 (Space / Esc)")) SkipTransition();
        }
        public void SkipTransition() { if(transitioning) EndTransition(true); }
        void EndTransition(bool complete)
        {
            if(!transitioning) return;
            transitioning=false;
            if(cinematicGame && cinematicGame.State==RunState.Cinematic)
            {
                cinematicGame.State=returnState;
                if(cinematicGame.Cam) { cinematicGame.Cam.transform.position=savedCameraPosition; cinematicGame.Cam.fieldOfView=savedFov; }
            }
            cinematicGame=null; Action=ActionState.Chase;
            if(complete) { Phase=2; PhaseTwoStarted.Invoke(); }
        }
        void StartPatternCooldown()
        {
            if(ActivePattern==Pattern.Barrage) barrageCooldown=Mathf.Max(0,Barrage.Cooldown);
            if(ActivePattern==Pattern.Whip) whipCooldown=Mathf.Max(0,Whip.Cooldown);
            if(ActivePattern==Pattern.Dynamite) dynamiteCooldown=Mathf.Max(0,Dynamite.Cooldown);
        }
        void BeginRecovery()
        {
            float seconds = ActivePattern == Pattern.MachineGun ? MachineGun.RecoverySeconds :
                ActivePattern == Pattern.Barrage ? Barrage.RecoverySeconds :
                ActivePattern == Pattern.Shotgun ? Shotgun.RecoverySeconds :
                ActivePattern == Pattern.Whip ? Whip.RecoverySeconds : Dynamite.RecoverySeconds;
            StartPatternCooldown();
            RestoreWhipMovement(); SetHeight(0);
            if(owner) owner.SetMeleeWarning(false,0);
            recoveryRemaining=Mathf.Max(0,seconds);
            Action=ActionState.Recovery;
            if(recoveryRemaining<=0) CancelAttack(false);
        }
        void CancelAttack(bool startCooldown = true)
        {
            if (gunRange) gunRange.enabled = false;
            CancelDodge();
            RestoreWhipMovement();
            if(startCooldown && Action!=ActionState.Recovery) StartPatternCooldown();
            recoveryRemaining=0;
            ActivePattern=Pattern.None; Action=ActionState.Chase; elapsed=0;
            SetHeight(0); if(owner) owner.SetMeleeWarning(false,0);
        }
        public override void Cancel() { EndTransition(false); CancelAttack(); }
        void OnDisable() { Cancel(); }
        void OnDestroy() { if(owner) owner.Damaged-=OnDamaged; if(gunRangeMaterial) Destroy(gunRangeMaterial); }
    }
}
