namespace WesternLemegeton.Passives
{
    public static class WesternPassiveStatKeys
    {
        public const string PlayerAttack = "ATK";
        public const string MaxHealth = "HP";
        public const string StigmaAttack = "StigmaATK";
        public const string MoveSpeed = "Move";
        public const string RavenAttack = "CrowATK";
        public const string RavenRange = "CrowRange";
        public const string CriticalDamage = "CritDamage";
        public const string CriticalRate = "CritRate";
        public const string StigmaDamage = "StigmaDamage";

        public static bool IsKnown(string key) =>
            key == PlayerAttack || key == MaxHealth || key == StigmaAttack ||
            key == MoveSpeed || key == RavenAttack || key == RavenRange ||
            key == CriticalDamage || key == CriticalRate || key == StigmaDamage;

        public static bool IsInitialRuntimeSupported(string key) =>
            key == PlayerAttack || key == MoveSpeed || key == RavenAttack;
    }
}
