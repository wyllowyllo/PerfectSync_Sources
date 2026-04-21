namespace Core.Utilities
{
    public static class DeterministicHash
    {
        public static int PickIndex(int seed, int count)
        {
            if (count <= 1) return 0;

            // Knuth multiplicative hash — 순차 seed의 모듈러 편향 완화.
            unchecked
            {
                uint hash = (uint)seed * 2654435761u;
                return (int)(hash % (uint)count);
            }
        }
    }
}
