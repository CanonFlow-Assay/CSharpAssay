namespace OneOf
{
    public readonly struct OneOf<T0>
    {
        private readonly T0 value;

        public OneOf(T0 value)
        {
            this.value = value;
        }

        public bool IsT0 => true;

        public T0 AsT0 => value;
    }
}

namespace TypeGym
{
    public static class Challenge
    {
        public static string Read(OneOf.OneOf<string> value) => value.AsT0;

        public static string Probe() => Read(new OneOf.OneOf<string>("migrated"));
    }
}
