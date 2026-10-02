namespace WesternLemegeton.Passives
{
    // The gameplay stat keys supported by Astra's passive hooks.
    public static class AstraPassiveStatKeys
    {
        public const string PlayerDamage = "PlayerDamage";
        public const string MoveSpeed = "MoveSpeed";
        public const string RavenDamage = "RavenDamage";

        public static bool IsSupported(string key) =>
            key == PlayerDamage || key == MoveSpeed || key == RavenDamage;
    }
}
