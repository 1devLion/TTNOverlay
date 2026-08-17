using System;

namespace TTNOverlay.Net.BrowserTls
{
    /// <summary>
    /// Provides GREASE (Generate Random Extensions And Sustain Extensibility) values
    /// as defined in RFC 8701 for TLS ClientHello randomization.
    /// </summary>
    public static class GreaseValues
    {
        private static readonly int[] AllGreaseValues =
        {
            0x0A0A, 0x1A1A, 0x2A2A, 0x3A3A, 0x4A4A, 0x5A5A, 0x6A6A, 0x7A7A,
            0x8A8A, 0x9A9A, 0xAAAA, 0xBABA, 0xCACA, 0xDADA, 0xEAEA, 0xFAFA,
        };

        /// <summary>
        /// A GREASE value used for the named group extension.
        /// </summary>
        public const int NamedGroupGrease = 0x0A0A;

        private static readonly Random Rng = new Random();

        /// <summary>
        /// Returns a random GREASE value.
        /// </summary>
        public static int Pick() => AllGreaseValues[Rng.Next(AllGreaseValues.Length)];
    }
}