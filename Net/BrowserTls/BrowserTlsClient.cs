using System.Collections.Generic;
using System.Linq;
using System.Text;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using TTNOverlay.Services;

namespace TTNOverlay.Net.BrowserTls
{
    /// <summary>
    /// A TlsClient implementation that mimics a browser TLS fingerprint
    /// for impersonating HTTP requests to Cloudflare-protected endpoints.
    /// </summary>
    public class BrowserTlsClient : DefaultTlsClient
    {
        private readonly string _host;
        private bool _certValid;

        public BrowserTlsClient(string host)
            : base(new BcTlsCrypto(new SecureRandom()))
        {
            _host = host;
        }

        /// <summary>
        /// Gets a value indicating whether the server certificate passed validation.
        /// </summary>
        public bool ServerCertificateValid => _certValid;

        protected override ProtocolVersion[] GetSupportedVersions() =>
            ProtocolVersion.TLSv13.DownTo(ProtocolVersion.TLSv12);

        protected override int[] GetSupportedCipherSuites()
        {
            var withGrease = new List<int> { GreaseValues.Pick() };
            withGrease.AddRange(ChromeFingerprintProfile.CipherSuites);
            return withGrease.ToArray();
        }

        protected override IList<ProtocolName> GetProtocolNames() =>
            new List<ProtocolName> { ProtocolName.Http_1_1 };

        protected override IList<ServerName> GetSniServerNames() =>
            new List<ServerName> { new ServerName(NameType.host_name, Encoding.ASCII.GetBytes(_host)) };

        protected override IList<SignatureAndHashAlgorithm> GetSupportedSignatureAlgorithms()
        {
            var list = new List<SignatureAndHashAlgorithm>();
            foreach (var scheme in ChromeFingerprintProfile.SignatureSchemes)
                list.Add(SignatureScheme.GetSignatureAndHashAlgorithm(scheme));
            return list;
        }

        protected override IList<int> GetSupportedGroups(IList<int> namedGroupRoles)
        {
            var withGrease = new List<int> { GreaseValues.NamedGroupGrease };
            withGrease.AddRange(ChromeFingerprintProfile.SupportedGroups.Where(g => g != 4588));
            return withGrease;
        }

        public override IList<int> GetEarlyKeyShareGroups() =>
            new List<int> { NamedGroup.x25519 };

        public override TlsAuthentication GetAuthentication() => new BrowserTlsAuthentication(this);

        private class BrowserTlsAuthentication : TlsAuthentication
        {
            private readonly BrowserTlsClient _owner;
            public BrowserTlsAuthentication(BrowserTlsClient owner) => _owner = owner;

            public void NotifyServerCertificate(TlsServerCertificate serverCertificate)
            {
                _owner._certValid = CertValidator.ValidateChain(serverCertificate.Certificate, _owner._host);
                if (!_owner._certValid)
                    DebugLog.Write($"BrowserTlsClient: {_owner._host} certificate did NOT validate against the Windows Store");
            }

            public TlsCredentials? GetClientCredentials(CertificateRequest certificateRequest) => null;
        }
    }
}