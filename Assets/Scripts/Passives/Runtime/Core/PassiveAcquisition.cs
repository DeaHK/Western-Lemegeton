namespace WesternLemegeton.Passives
{
    public enum PassiveAcquisitionSource
    {
        Unknown = 0,
        EntranceChoice = 1,
        ShopPurchase = 2,
        MonsterDrop = 3,
        BossReward = 4,
        EventReward = 5,
        Debug = 6
    }

    public enum PassiveAcquireKind
    {
        Failed = 0,
        Added = 1,
        Stacked = 2
    }

    // Immutable acquisition snapshot; Instance remains the existing runtime object.
    public readonly struct PassiveAcquireResult
    {
        private readonly string error;

        public PassiveAcquireKind Kind { get; }
        public PassiveInstance Instance { get; }
        public PassiveAcquisitionSource Source { get; }
        public int PreviousStack { get; }
        public int NewStack { get; }
        public string Error => error ?? (Success ? string.Empty : "Passive acquisition failed.");
        public bool Success => Kind == PassiveAcquireKind.Added || Kind == PassiveAcquireKind.Stacked;
        public bool WasAdded => Kind == PassiveAcquireKind.Added;
        public bool WasStacked => Kind == PassiveAcquireKind.Stacked;

        internal PassiveAcquireResult(PassiveAcquireKind kind, PassiveInstance instance,
            PassiveAcquisitionSource source, int previousStack, int newStack, string error)
        {
            Kind = kind;
            Instance = instance;
            Source = source;
            PreviousStack = previousStack;
            NewStack = newStack;
            this.error = error;
        }
    }
}
