using System;

namespace Hyperletter.Extension {
    public static class FlagsEnumExtensions {
        public static bool IsSet<T>(this Enum type, T value) {
            ulong keysVal = Convert.ToUInt64(type);
            ulong flagVal = Convert.ToUInt64(value);

            return (keysVal & flagVal) == flagVal;
        }
    }
}
