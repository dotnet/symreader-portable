// should be higher than compression threshold (200 chars)

using System;

namespace Test
{
    public static class SomeCode
    {
        public static int SomeMethod(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return checked(value + 42);
        }
    }
}
