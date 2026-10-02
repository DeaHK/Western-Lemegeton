namespace WesternLemegeton.Passives
{
    public sealed class PassiveEffectContext
    {
        public PassiveManager Manager { get; }
        public PassiveRuntimeContext Runtime { get; }

        public PassiveEffectContext(
            PassiveManager manager,
            PassiveRuntimeContext runtime)
        {
            Manager = manager;
            Runtime = runtime;
        }

        public bool TryGetService<T>(out T service) where T : class
        {
            if (Runtime == null)
            {
                service = null;
                return false;
            }

            return Runtime.TryGetService(out service);
        }

        public T GetService<T>() where T : class
        {
            return Runtime != null ? Runtime.GetService<T>() : null;
        }
    }
}
