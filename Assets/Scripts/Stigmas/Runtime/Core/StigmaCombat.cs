using System;
using System.Collections.Generic;
using UnityEngine;
using WesternLemegeton.Passives;

namespace WesternLemegeton.Stigmas
{
    public enum StigmaAttackKind { Basic, Skill, Followup, Link, Familiar, DamageOverTime }

    public static class StigmaStats
    {
        public const string PlayerSpeed = "PlayerAttackSpeed", RavenSpeed = "RavenAttackSpeed", RavenMove = "RavenMove";
        public const string DashCooldown = "DashCooldown", HitStun = "HitStun", BonusGain = "BonusHpGain";
        public static string Attack(StigmaOwner owner) => owner == StigmaOwner.Player ? WesternPassiveStatKeys.PlayerAttack : WesternPassiveStatKeys.RavenAttack;
        public static string Speed(StigmaOwner owner) => owner == StigmaOwner.Player ? PlayerSpeed : RavenSpeed;
        public static string Move(StigmaOwner owner) => owner == StigmaOwner.Player ? WesternPassiveStatKeys.MoveSpeed : RavenMove;
    }

    // Effect execution is independent of equipment storage and actor input.
    public sealed class StigmaCombat
    {
        sealed class TimedModifier { public string Key; public float Remaining; public Action Finished; }
        readonly StigmaManager manager;
        readonly Dungeon game;
        readonly HashSet<string> modifierKeys = new HashSet<string>();
        readonly List<TimedModifier> timers = new List<TimedModifier>();
        readonly List<string> shieldSources = new List<string>();
        readonly Dictionary<string, Func<StigmaOwner, string>> statModules = new Dictionary<string, Func<StigmaOwner, string>> {
            { "move_percent", StigmaStats.Move }, { "attack_percent", StigmaStats.Attack },
            { "dash_cooldown_percent", _ => StigmaStats.DashCooldown }, { "max_hp_percent", _ => WesternPassiveStatKeys.MaxHealth },
            { "hit_stun_percent", _ => StigmaStats.HitStun }, { "bonus_hp_gain_percent", _ => StigmaStats.BonusGain }
        };
        readonly int[] killChains = new int[2];
        readonly float[] chainRemaining = new float[2];
        bool shieldBuffActive;
        public int Charges { get; private set; }
        public int EffectRevision { get; private set; }
        public BonusHealth Shield => game.Hero.BonusHp;
        public StigmaLinkSkills Skills { get; }
        public StigmaRulesSO Rules => manager.Database.Rules;
        public bool Active => manager.Loadout.BothEquipped;
        public int Level => manager.Loadout.EffectiveLevel;
        public StigmaCombinationSO Combination => manager.Loadout.Combination;
        public Dungeon Game => game;
        public static bool LeavesTrace(StigmaOwner owner, StigmaAttackKind kind) =>
            kind == StigmaAttackKind.Basic || (owner == StigmaOwner.Player && kind == StigmaAttackKind.Skill);

        public StigmaCombat(StigmaManager manager, Dungeon game)
        {
            this.manager = manager; this.game = game;
            Skills = new StigmaLinkSkills(this);
            Rebuild();
        }
        public static float Parameter(StigmaParameter[] parameters, string key, float fallback = 0)
        {
            foreach (var p in parameters) if (p.Key == key) return p.Value;
            return fallback;
        }
        public float Effect(StigmaOwner owner, string module, string key, float fallback = 0)
        {
            if (!Active) return 0;
            var effect = manager.Loadout.Get(owner).Definition.Effect(manager.Loadout.EffectLevel(owner), module);
            return effect == null ? 0 : Parameter(effect.Parameters, key, fallback);
        }
        public float Bonus(string module, string key = "value")
        {
            if (!Active || !Combination) return 0;
            foreach (var effect in Combination.Level(Level).Bonuses)
                if (effect.ModuleId == module) return Parameter(effect.Parameters, key);
            return 0;
        }
        public void Changed(StigmaChange change)
        {
            if (change.Kind == StigmaChangeKind.Acquired) return;
            EffectRevision++;
            ClearEffects();
            Charges = change.Kind == StigmaChangeKind.Reset ? 0 : Rules.ClampChargeAfterReplacement(Charges, manager.Loadout.ChargeCapacity);
            Rebuild();
        }
        public void SetCharges(int value) => Charges = Mathf.Clamp(value, 0, manager.Loadout.ChargeCapacity);
        public bool SpendCharges(int value) { if (value <= 0 || Charges < value) return false; Charges -= value; return true; }
        public void Modifier(string source, string stat, float value, float seconds = 0, Action finished = null)
        {
            string key = "stigma/" + source;
            game.PassiveStats.SetModifier(key, stat, value, PassiveValueType.Percent); modifierKeys.Add(key);
            timers.RemoveAll(t => t.Key == key);
            if (seconds > 0) timers.Add(new TimedModifier { Key = key, Remaining = seconds, Finished = finished });
        }
        public void RemoveModifier(string source)
        {
            string key = "stigma/" + source;
            game.PassiveStats.RemoveModifier(key); modifierKeys.Remove(key); timers.RemoveAll(t => t.Key == key);
        }
        public void ShieldSource(string source, float cap)
        {
            string key = "stigma/" + source;
            if (!shieldSources.Contains(key)) shieldSources.Add(key);
            Shield.SetSource(key, cap);
        }
        public float GrantAmount(float amount) => Mathf.Max(0, game.PassiveStats.Evaluate(StigmaStats.BonusGain, amount));
        public void GrantShield(string source, float amount) { Shield.Grant("stigma/" + source, GrantAmount(amount)); SyncShieldBuff(); }
        public void SyncShieldBuff()
        {
            bool active = Active && Shield.Current > 0;
            if (active == shieldBuffActive) return;
            shieldBuffActive = active;
            if (active)
            {
                float reduction = Bonus("shield_stun_reduction");
                if (reduction != 0) Modifier("shield/stun", StigmaStats.HitStun, -reduction);
                if (Skills.BloomActive) Modifier("bloom/attack", WesternPassiveStatKeys.PlayerAttack, Skills.BloomAttackPercent);
            }
            else { RemoveModifier("shield/stun"); RemoveModifier("bloom/attack"); }
        }
        public void Rebuild()
        {
            if (!Active) return;
            foreach (StigmaOwner owner in Enum.GetValues(typeof(StigmaOwner)))
            {
                var data = manager.Loadout.Get(owner).Definition.Level(manager.Loadout.EffectLevel(owner));
                ApplyStats(manager.Loadout.Get(owner).Definition.CommonEffects, owner, "common/" + owner, false);
                ApplyStats(data.Effects, owner, "owner/" + owner, false);
            }
            if (Combination)
            {
                var effects = Combination.Level(Level).Bonuses;
                ApplyStats(effects, StigmaOwner.Player, "combination/player", false);
                ApplyStats(effects, StigmaOwner.Raven, "combination/raven", true);
            }
            foreach (StigmaOwner owner in Enum.GetValues(typeof(StigmaOwner)))
            {
                float amount = Effect(owner, "on_kill_bonus_hp", "amount");
                if (amount > 0) ShieldSource("kill/" + owner, GrantAmount(amount));
            }
            game.Hero.Hp = Mathf.Min(game.Hero.Hp, game.Hero.MaxHealth);
            SyncShieldBuff();
        }
        void ApplyStats(StigmaEffectDefinition[] effects, StigmaOwner owner, string source, bool actorOnly)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                var e = effects[i];
                if (actorOnly && e.ModuleId != "move_percent" && e.ModuleId != "attack_percent") continue;
                if (statModules.TryGetValue(e.ModuleId, out var stat)) Modifier(source + "/" + i, stat(owner), Parameter(e.Parameters, "percent"));
            }
        }
        public void Hit(Enemy enemy, StigmaOwner owner, StigmaAttackKind kind, float damage)
        {
            if (!enemy || enemy.Dead || !game.Running || game.IsTown) return;
            var status = enemy.Stigma;
            bool leavesTrace = LeavesTrace(owner, kind);
            if (Active && leavesTrace)
                status.Mark(owner, Effect(owner, "marked_enemy_slow", "percent") / 100f,
                    Effect(owner, "slowed_enemy_vulnerability", "percent") > 0);
            bool slowed = status.Slow(Rules.MaximumSlowFraction) > 0;
            bool vulnerable = slowed && (status.Vulnerable ||
                Effect(StigmaOwner.Player, "slowed_enemy_vulnerability", "percent") > 0 ||
                Effect(StigmaOwner.Raven, "slowed_enemy_vulnerability", "percent") > 0);
            if (Active && leavesTrace && status.Marks == 3 &&
                (Rules.MaxChargeGrantsPerEnemy == 0 || status.ChargeGrants < Rules.MaxChargeGrantsPerEnemy) && Charges < manager.Loadout.ChargeCapacity)
            { status.ConvertMarks(); Charges++; }
            enemy.TakeStigmaDamage(Mathf.Max(0, damage) * (vulnerable ? 1.1f : 1), owner, slowed);
            if (Active && !enemy.Dead && kind != StigmaAttackKind.DamageOverTime &&
                Effect(owner, "on_hit_knockback", "distance", Rules.KnockbackDistance) > 0)
            {
                Vector2 origin = owner == StigmaOwner.Player ? game.Pos : (Vector2)game.Crow.transform.position;
                Vector2 at = enemy.transform.position;
                enemy.transform.position = WesternLemegeton.Rules.ResolveObstacles(at, at + (at - origin).normalized * Effect(owner, "on_hit_knockback", "distance", Rules.KnockbackDistance), .5f);
            }
        }
        public void Killed(StigmaOwner owner, bool slowed)
        {
            if (!Active) return;
            float speed = Effect(owner, "on_kill_attack_speed", "percent");
            if (speed > 0) Modifier("kill/speed/" + owner, StigmaStats.Speed(owner), speed, Effect(owner, "on_kill_attack_speed", "seconds"));
            float shield = Effect(owner, "on_kill_bonus_hp", "amount");
            if (shield > 0) GrantShield("kill/" + owner, shield);
            if (slowed) game.Hero.DashCd = Mathf.Max(0, game.Hero.DashCd - Bonus("slowed_kill_dash_refund"));
            float step = Bonus("kill_chain_speed");
            if (step > 0)
            {
                int index = (int)owner;
                killChains[index] = Mathf.Min(Mathf.Max(1, Rules.KillChainMaxStacks), killChains[index] + 1);
                chainRemaining[index] = Rules.KillChainSeconds;
                Modifier("chain/" + owner, StigmaStats.Speed(owner), killChains[index] * step, Rules.KillChainSeconds);
            }
            if (Skills.BloomActive && Level == 3) GrantShield("bloom", Rules.BloomKillRefill);
        }
        public void Tick(float dt)
        {
            List<Action> finished = null;
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                var timer = timers[i]; timer.Remaining -= dt;
                if (timer.Remaining > 0) continue;
                timers.RemoveAt(i); game.PassiveStats.RemoveModifier(timer.Key); modifierKeys.Remove(timer.Key);
                if (timer.Finished != null) { if (finished == null) finished = new List<Action>(); finished.Add(timer.Finished); }
            }
            if (finished != null) foreach (var callback in finished) callback();
            for (int i = 0; i < 2; i++) { chainRemaining[i] -= dt; if (chainRemaining[i] <= 0) killChains[i] = 0; }
            Skills.Tick(dt); SyncShieldBuff();
        }
        public void ClearEffects()
        {
            Skills.Clear(); timers.Clear();
            foreach (var key in modifierKeys) game.PassiveStats.RemoveModifier(key);
            modifierKeys.Clear();
            foreach (var source in shieldSources) Shield.RemoveSource(source);
            shieldSources.Clear(); shieldBuffActive = false;
            Array.Clear(killChains, 0, 2); Array.Clear(chainRemaining, 0, 2);
            foreach (var enemy in game.Enemies) if (enemy) enemy.Stigma.ClearEffects();
            game.Hero.Hp = Mathf.Min(game.Hero.Hp, game.Hero.MaxHealth);
        }
        public void OnDeath() { ClearEffects(); Charges = 0; }
    }
}
