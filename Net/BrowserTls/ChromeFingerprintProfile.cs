using System;
using System.Collections.Generic;
using System.Linq;

namespace TTNOverlay.Net.BrowserTls
{
    /// <summary>
    /// Provides raw TLS and HTTP fingerprint data for impersonating Chrome 131 on Windows.
    /// </summary>
    public static class ChromeFingerprintProfile
    {
        private const string Ja3Chrome131Windows =
            "772,4865-4866-4867-49195-49199-49196-49200-52393-52392-49171-49172-156-157-47-53," +
            "45-0-65037-17513-35-10-13-65281-16-51-23-27-18-43-11-5,4588-29-23-24,0";

        /// <summary>
        /// TLS cipher suites supported by Chrome 131.
        /// </summary>
        public static readonly int[] CipherSuites;

        /// <summary>
        /// Order of extension types used in the ClientHello.
        /// </summary>
        public static readonly int[] ExtensionOrder;

        /// <summary>
        /// Supported named groups (curves) for key exchange.
        /// </summary>
        public static readonly int[] SupportedGroups;

        /// <summary>
        /// Early key share groups sent in the ClientHello.
        /// </summary>
        public static readonly int[] EarlySharedGroups = { GreaseValues.NamedGroupGrease, 4588, 29 };

        /// <summary>
        /// Signature schemes supported by Chrome 131.
        /// </summary>
        public static readonly int[] SignatureSchemes =
        {
            0x0403, 0x0804, 0x0401, 0x0503, 0x0805, 0x0501, 0x0806, 0x0601
        };

        /// <summary>
        /// ALPN protocol list.
        /// </summary>
        public static readonly string[] AlpnProtocols = { "h2", "http/1.1" };

        /// <summary>
        /// User-Agent string for Chrome 131.
        /// </summary>
        public const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

        /// <summary>
        /// HTTP headers sent by Chrome in a navigation request, in order.
        /// </summary>
        public static readonly (string Name, string Value)[] OrderedHeaders =
        {
            ("sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\""),
            ("sec-ch-ua-mobile", "?0"),
            ("sec-ch-ua-platform", "\"Windows\""),
            ("Upgrade-Insecure-Requests", "1"),
            ("User-Agent", UserAgent),
            ("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
            ("Sec-Fetch-Site", "none"),
            ("Sec-Fetch-Mode", "navigate"),
            ("Sec-Fetch-User", "?1"),
            ("Sec-Fetch-Dest", "document"),
            ("Accept-Encoding", "gzip, deflate, br"),
            ("Accept-Language", "en-US,en;q=0.9"),
        };

        static ChromeFingerprintProfile()
        {
            var parts = Ja3Chrome131Windows.Split(',');
            CipherSuites   = ParseGroup(parts[1]);
            ExtensionOrder = ParseGroup(parts[2]);
            SupportedGroups = ParseGroup(parts[3]);
        }

        private static int[] ParseGroup(string group) =>
            group.Length == 0
                ? Array.Empty<int>()
                : group.Split('-').Select(int.Parse).ToArray();
    }
}