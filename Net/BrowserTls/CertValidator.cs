using System;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Tls;
using TTNOverlay.Services;

namespace TTNOverlay.Net.BrowserTls
{
    /// <summary>
    /// Validates a server certificate chain against the Windows certificate store
    /// and checks the host name.
    /// </summary>
    public static class CertValidator
    {
        /// <summary>
        /// Validates the provided server certificate chain and host name.
        /// </summary>
        /// <param name="serverCertificate">The server's certificate chain.</param>
        /// <param name="expectedHost">The expected host name.</param>
        /// <returns>True if the chain is valid and the host name matches; otherwise, false.</returns>
        public static bool ValidateChain(Certificate serverCertificate, string expectedHost)
        {
            var certs = serverCertificate.GetCertificateList();
            if (certs.Length == 0)
            {
                DebugLog.Write("CertValidator: server did not send certificates");
                return false;
            }

            try
            {
                var leaf = X509CertificateLoader.LoadCertificate(certs[0].GetEncoded());

                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

                // Add intermediate certificates provided by the server.
                for (int i = 1; i < certs.Length; i++)
                {
                    chain.ChainPolicy.ExtraStore.Add(X509CertificateLoader.LoadCertificate(certs[i].GetEncoded()));
                }

                bool chainOk = chain.Build(leaf);

                if (!chainOk)
                {
                    foreach (var status in chain.ChainStatus)
                        DebugLog.Write($"CertValidator: chain status -> {status.Status} ({status.StatusInformation})");
                    return false;
                }

                // Verify that the host name matches the certificate's subject or SAN.
                bool hostMatches = leaf.GetNameInfo(X509NameType.DnsName, false)
                    .Equals(expectedHost, StringComparison.OrdinalIgnoreCase)
                    || leaf.Extensions["2.5.29.17"] != null; // SAN extension present.

                return hostMatches;
            }
            catch (Exception ex)
            {
                DebugLog.Write($"CertValidator: EXCEPTION validating string: {ex}");
                return false;
            }
        }
    }
}