using System;
using System.Collections.Generic;
using UnityEngine;
using WesternLemegeton.Passives;

namespace WesternLemegeton.Stigmas
{
    public sealed class StigmaLinkSkills
    {
        sealed class Familiar { public SpriteRenderer View; public float Remaining, AttackTimer, Damage; public bool Explodes; }
        sealed class Ground { public SpriteRenderer View; public Vector2 Position; public float Radius, Remaining, TickTimer, Damage; }
        readonly StigmaCombat combat;
        readonly Dictionary<string, Action<Vector2>> modules;
        readonly List<Familiar> familiars = new List<Familiar>();
        readonly List<Ground> grounds = new List<Ground>();
        public bool BloomActive { get; private set; }
        public float BloomAttackPercent { get; private set; }
        public int FamiliarCount => familiars.Count;
        public int GroundCount => grounds.Count;
        Dungeon Game => combat.Game;
        StigmaRulesSO Rules => combat.Rules;
        int Tier => Mathf.Clamp(combat.Level - 1, 0, 2);
        public StigmaLinkSkills(StigmaCombat combat)
        {
            this.combat = combat;
            modules = new Dictionary<string, Action<Vector2>> {
                { "flood", Flood }, { "bloom", Bloom }, { "burn_rush", Rush },
                { "vapor", Vapor }, { "mire", Mire }, { "inferno", Inferno }
            };
        }
        // Unspecified higher-tier parameters inherit their lower-tier defined value.
        float P(string key, float fallback = 0) => combat.Combination.SkillParameter(combat.Level, key, fallback);
        public bool TryCast(Vector2 target, out string error)
        {
            error = null;
            if (!Game.Running || Game.IsTown || !combat.Active || !combat.Combination) { error = "두 성흔을 장착한 전투 지역에서 사용하세요."; return false; }
            if (Game.Hero.SkillBusy || Game.Hero.BasicBusy || Game.Hero.Dodging || Game.Hero.Stunned) { error = "현재 행동이 끝난 뒤 사용하세요."; return false; }
            if (!modules.TryGetValue(combat.Combination.SkillModuleId, out var cast)) { error = "등록되지 않은 연계스킬입니다."; return false; }
            float hpCost = P("max_hp_cost_percent") * Game.Hero.MaxHealth / 100f;
            if (Game.Hero.Hp - hpCost < P("minimum_remaining_hp", 1)) { error = "대가를 지불할 기본 체력이 부족합니다."; return false; }
            if (!combat.SpendCharges(combat.Combination.ChargeCost)) { error = "성흔 표식이 부족합니다."; return false; }
            Game.Hero.Hp -= hpCost;
            cast(target); Game.Sound(true);
            return true;
        }
        float Damage(float percent) => Game.PassiveStats.Evaluate(WesternPassiveStatKeys.StigmaDamage,
            Game.PassiveStats.Evaluate(WesternPassiveStatKeys.StigmaAttack,
                Game.PassiveStats.Evaluate(WesternPassiveStatKeys.PlayerAttack, Rules.LinkBaseAttack)) * percent / 100f);
        void Area(Vector2 point, float radius, float damage, StigmaAttackKind kind = StigmaAttackKind.Link)
        {
            foreach (var enemy in Game.Enemies)
                if (enemy && !enemy.Dead && Vector2.Distance(point, enemy.transform.position) <= radius)
                    combat.Hit(enemy, StigmaOwner.Player, kind, damage);
        }
        void Flood(Vector2 _)
        {
            float radius = Rules.FloodRadius[Tier], seconds = Rules.FloodSeconds[Tier];
            foreach (var enemy in Game.Enemies)
                if (enemy && !enemy.Dead && Vector2.Distance(Game.Pos, enemy.transform.position) <= radius)
                    enemy.Stigma.Debuff(P("slow_percent", 40) / 100f, seconds, combat.Level == 3);
            Ink.Ring(Game.Pos, radius, Ink.Cyan, .5f);
        }
        void Bloom(Vector2 _)
        {
            BloomActive = true; BloomAttackPercent = P("attack_percent", 10);
            float amount = P("bonus_hp", 15);
            combat.ShieldSource("bloom", combat.GrantAmount(amount)); combat.GrantShield("bloom", amount);
            if (combat.Shield.Current > 0) combat.Modifier("bloom/attack", WesternPassiveStatKeys.PlayerAttack, BloomAttackPercent);
            Ink.Ring(Game.Pos, 1.5f, new Color(.8f, 1, .4f), .5f);
        }
        void Rush(Vector2 _)
        {
            float duration = combat.Level == 3 ? Rules.RushLevel3Seconds : P("seconds", 3);
            float penaltyDuration = combat.Level == 3 ? Rules.RushLevel3PenaltySeconds : P("penalty_seconds", 1.5f);
            float penalty = P("penalty_slow_percent", 15);
            combat.RemoveModifier("rush/penalty");
            combat.Modifier("rush/player", StigmaStats.PlayerSpeed, P("attack_speed_percent", 30), duration,
                () => combat.Modifier("rush/penalty", WesternPassiveStatKeys.MoveSpeed, -penalty, penaltyDuration));
            combat.Modifier("rush/raven", StigmaStats.RavenSpeed, P("attack_speed_percent", 30), duration);
            Ink.Ring(Game.Pos, 2, Ink.Gold, .5f);
        }
        void Vapor(Vector2 _)
        {
            ClearFamiliars();
            int count = Mathf.Clamp(Mathf.RoundToInt(P("familiar_count", 1)), 1, 8);
            for (int i = 0; i < count; i++)
            {
                var view = Ink.Shape("Stigma Familiar", null, Game.Pos + new Vector2(i == 0 ? -1 : 1, 1), Vector2.one * .5f, Ink.Cyan, 1000, true);
                familiars.Add(new Familiar { View = view, Remaining = P("seconds", 6), Damage = Damage(P("attack_percent", 40)), Explodes = combat.Level == 3 });
            }
            combat.Modifier("vapor/penalty", WesternPassiveStatKeys.MoveSpeed, -P("penalty_slow_percent", 10));
        }
        void Mire(Vector2 target)
        {
            Vector2 direction = target - Game.Pos;
            if (direction.sqrMagnitude < .01f) direction = Game.Hero.Facing;
            Vector2 point = Game.Pos + direction.normalized * Rules.MireForwardDistance;
            float radius = Rules.MireRadius[Tier];
            foreach (var enemy in Game.Enemies)
                if (enemy && !enemy.Dead && Vector2.Distance(point, enemy.transform.position) <= radius)
                    combat.Hit(enemy, StigmaOwner.Player, StigmaAttackKind.Link,
                        Damage(enemy.Stigma.Slow(Rules.MaximumSlowFraction) > 0 ? P("slowed_attack_percent", 220) : P("attack_percent", 80)));
            combat.Modifier("mire/penalty", WesternPassiveStatKeys.MoveSpeed, -P("penalty_slow_percent", 30), P("penalty_seconds", 3));
            Ink.Ring(point, radius, new Color(.4f, .65f, .3f), .5f);
        }
        void Inferno(Vector2 target)
        {
            Vector2 point = Game.Pos + Vector2.ClampMagnitude(target - Game.Pos, Rules.InfernoTargetRange);
            point = WesternLemegeton.Rules.OutsideCover(point, .1f);
            float radius = Rules.InfernoRadius[Tier];
            Area(point, radius, Damage(P("attack_percent", 350)));
            Ink.Ring(point, radius, Ink.Gold, .55f);
            if (combat.Level == 3)
            {
                var view = Ink.Shape("Stigma Flame Ground", null, point, Vector2.one * radius * 2, new Color(1, .35f, .1f, .25f), -50, true);
                grounds.Add(new Ground { View = view, Position = point, Radius = radius, Remaining = P("ground_seconds", 3), Damage = Damage(Rules.GroundAttackPercent) });
            }
        }
        public void Tick(float dt)
        {
            for (int i = familiars.Count - 1; i >= 0; i--)
            {
                var f = familiars[i]; f.Remaining -= dt; f.AttackTimer -= dt;
                if (f.Remaining <= 0)
                {
                    if (f.Explodes && f.View) { Area(f.View.transform.position, Rules.FamiliarExplosionRadius, Damage(Rules.FamiliarExplosionPercent), StigmaAttackKind.Familiar); Ink.Ring(f.View.transform.position, Rules.FamiliarExplosionRadius, Ink.Cyan); }
                    if (f.View) UnityEngine.Object.Destroy(f.View.gameObject);
                    familiars.RemoveAt(i); continue;
                }
                var target = Game.Nearest(Game.Pos, Rules.FamiliarRange);
                Vector2 destination = target ? (Vector2)target.transform.position : Game.Pos + new Vector2(i == 0 ? -1 : 1, 1);
                if (f.View) f.View.transform.position = Vector2.MoveTowards(f.View.transform.position, destination, dt * 7);
                if (target && f.View && Vector2.Distance(f.View.transform.position, target.transform.position) < 1.2f && f.AttackTimer <= 0)
                {
                    f.AttackTimer = Mathf.Max(.1f, Rules.FamiliarInterval);
                    combat.Hit(target, StigmaOwner.Player, StigmaAttackKind.Familiar, f.Damage);
                    Ink.Line(f.View.transform.position, target.transform.position, Ink.Cyan, .1f, .2f);
                }
            }
            if (familiars.Count == 0) combat.RemoveModifier("vapor/penalty");
            for (int i = grounds.Count - 1; i >= 0; i--)
            {
                var field = grounds[i]; field.Remaining -= dt; field.TickTimer -= dt;
                if (field.Remaining <= 0) { if (field.View) UnityEngine.Object.Destroy(field.View.gameObject); grounds.RemoveAt(i); continue; }
                if (field.TickTimer <= 0) { field.TickTimer = Mathf.Max(.1f, Rules.GroundTickSeconds); Area(field.Position, field.Radius, field.Damage, StigmaAttackKind.DamageOverTime); }
            }
        }
        void ClearFamiliars()
        {
            foreach (var f in familiars) if (f.View) UnityEngine.Object.Destroy(f.View.gameObject);
            familiars.Clear(); combat.RemoveModifier("vapor/penalty");
        }
        public void ClearWorldObjects()
        {
            ClearFamiliars();
            foreach (var field in grounds) if (field.View) UnityEngine.Object.Destroy(field.View.gameObject);
            grounds.Clear();
        }
        public void Clear() { ClearWorldObjects(); BloomActive = false; BloomAttackPercent = 0; }
    }
}
