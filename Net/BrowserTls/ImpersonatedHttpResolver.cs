using System;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Org.BouncyCastle.Tls;
using TTNOverlay.Services;

namespace TTNOverlay.Net.BrowserTls
{
    /// <summary>
    /// Performs a single HTTPS GET request using a TLS ClientHello that impersonates a browser,
    /// to bypass fingerprint-based blocking.
    /// </summary>
    public static class ImpersonatedHttpResolver
    {
        /// <summary>
        /// Sends a GET request to the specified host and path, and returns the response body.
        /// </summary>
        /// <param name="host">The target host name.</param>
        /// <param name="path">The request path.</param>
        /// <returns>The response body as a string, or null on failure.</returns>
        public static async Task<string?> GetAsync(string host, string path)
        {
            TcpClient? tcp = null;
            TlsClientProtocol? protocol = null;

            try
            {
                DebugLog.Write($"ImpersonatedHttpResolver: connecting TCP to {host}:443...");
                tcp = new TcpClient();
                await tcp.ConnectAsync(host, 443);

                protocol = new TlsClientProtocol(tcp.GetStream());
                var client = new BrowserTlsClient(host);

                DebugLog.Write("ImpersonatedHttpResolver: starting impersonated TLS handshake...");
                protocol.Connect(client);

                if (!client.ServerCertificateValid)
                {
                    DebugLog.Write("ImpersonatedHttpResolver: invalid certificate, aborting");
                    return null;
                }

                var tlsStream = protocol.Stream;

                string request = BuildRequest(host, path);
                byte[] requestBytes = Encoding.ASCII.GetBytes(request);
                await tlsStream.WriteAsync(requestBytes, 0, requestBytes.Length);
                await tlsStream.FlushAsync();

                using var response = new MemoryStream();
                var buffer = new byte[8192];
                int read;
                try
                {
                    while ((read = await tlsStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        response.Write(buffer, 0, read);
                }
                catch (TlsNoCloseNotifyException)
                {
                    DebugLog.Write("ImpersonatedHttpResolver: closing without close_notify (expected), using what has already been read");
                }

                string body = ParseBody(response.ToArray());
                return body;
            }
            catch (Exception ex)
            {
                DebugLog.Write($"ImpersonatedHttpResolver: EXCEPTION: {ex}");
                return null;
            }
            finally
            {
                protocol?.Close();
                tcp?.Close();
                tcp?.Dispose();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                DebugLog.Write("ImpersonatedHttpResolver: released resources");
            }
        }

        private static string BuildRequest(string host, string path)
        {
            var sb = new StringBuilder();
            sb.Append($"GET {path} HTTP/1.1\r\n");
            sb.Append($"Host: {host}\r\n");
            foreach (var (name, value) in ChromeFingerprintProfile.OrderedHeaders)
                sb.Append($"{name}: {value}\r\n");
            sb.Append("Connection: close\r\n");
            sb.Append("\r\n");
            return sb.ToString();
        }

        private static string ParseBody(byte[] raw)
        {
            string headerText = Encoding.ASCII.GetString(raw, 0, System.Math.Min(raw.Length, 4096));
            int headerEnd = IndexOfDoubleCrlf(raw);
            if (headerEnd < 0)
                throw new InvalidDataException("The end of HTTP headers was not found");

            string statusLine = headerText.Split("\r\n")[0];
            DebugLog.Write($"ImpersonatedHttpResolver: response -> {statusLine}");

            byte[] bodyBytes = new byte[raw.Length - headerEnd - 4];
            Array.Copy(raw, headerEnd + 4, bodyBytes, 0, bodyBytes.Length);

            if (headerText.Contains("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase))
                bodyBytes = DecodeChunked(bodyBytes);

            if (headerText.Contains("Content-Encoding: br", StringComparison.OrdinalIgnoreCase))
            {
                using var input = new MemoryStream(bodyBytes);
                using var brotli = new BrotliStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                brotli.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
            if (headerText.Contains("Content-Encoding: gzip", StringComparison.OrdinalIgnoreCase))
            {
                using var input = new MemoryStream(bodyBytes);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                gzip.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
            if (headerText.Contains("Content-Encoding: zstd", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Compressed response with zstd, not yet supported");
            }

            return Encoding.UTF8.GetString(bodyBytes);
        }

        private static byte[] DecodeChunked(byte[] data)
        {
            using var output = new MemoryStream();
            int pos = 0;
            while (pos < data.Length)
            {
                int lineEnd = IndexOfCrlf(data, pos);
                if (lineEnd < 0) break;

                string sizeLine = Encoding.ASCII.GetString(data, pos, lineEnd - pos);
                string sizeHex = sizeLine.Split(';')[0].Trim();
                if (!int.TryParse(sizeHex, System.Globalization.NumberStyles.HexNumber, null, out int chunkSize))
                    break;

                int chunkStart = lineEnd + 2;
                if (chunkSize == 0) break;

                if (chunkStart + chunkSize > data.Length) break;
                output.Write(data, chunkStart, chunkSize);
                pos = chunkStart + chunkSize + 2;
            }
            return output.ToArray();
        }

        private static int IndexOfCrlf(byte[] data, int start)
        {
            for (int i = start; i < data.Length - 1; i++)
            {
                if (data[i] == '\r' && data[i + 1] == '\n')
                    return i;
            }
            return -1;
        }

        private static int IndexOfDoubleCrlf(byte[] data)
        {
            for (int i = 0; i < data.Length - 3; i++)
            {
                if (data[i] == '\r' && data[i + 1] == '\n' && data[i + 2] == '\r' && data[i + 3] == '\n')
                    return i;
            }
            return -1;
        }
    }
}