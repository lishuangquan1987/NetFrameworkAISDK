using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetFrameworkAISDK.Common;
using Org.BouncyCastle.Crypto.Tls;

namespace NetFrameworkAISDK.Common
{
    /// <summary>
    /// XP 专用 HTTPS 代理：利用 BouncyCastle 纯 C# 实现的 TLS 1.2，
    /// 在本地将 HTTP 请求升级为 TLS 1.2 转发到目标服务器。
    /// 仅在 TLS 1.2 不被 OS 支持时由 HttpClientBase 自动启动。
    /// </summary>
    internal class BouncyCastleTlsProxy : IDisposable
    {
        private static readonly object _lock = new object();
        private static BouncyCastleTlsProxy _instance;

        private static readonly ILogger _logger = new FileLogger();

        private TcpListener _listener;
        private Thread _listenThread;
        private int _port;
        private volatile bool _running;

        public static BouncyCastleTlsProxy Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new BouncyCastleTlsProxy();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>代理监听端口（由系统自动分配）</summary>
        public int Port { get { return _port; } }

        /// <summary>代理是否已启动</summary>
        public bool IsRunning { get { return _running; } }

        private BouncyCastleTlsProxy()
        {
        }

        /// <summary>启动代理（后台线程监听 127.0.0.1:随机端口）</summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_running) return;

                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                _running = true;

                _listenThread = new Thread(ListenLoop);
                _listenThread.IsBackground = true;
                _listenThread.Start();
            }
        }

        /// <summary>停止代理</summary>
        public void Stop()
        {
            _running = false;
            try { _listener.Stop(); } catch { }
        }

        private void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch
                {
                    if (_running) Thread.Sleep(100);
                }
            }
        }

        private void HandleClient(object state)
        {
            TcpClient client = (TcpClient)state;
            try
            {
                using (client)
                {
                    client.ReceiveTimeout = 30000;
                    client.SendTimeout = 30000;
                    using (NetworkStream clientStream = client.GetStream())
                    {
                        // 1. 读取完整的 HTTP 请求
                        string request = ReadHttpRequest(clientStream);
                        if (string.IsNullOrEmpty(request)) return;

                        // 2. 解析 Host 头和端口
                        string host;
                        int port;
                        if (!ParseHost(request, out host, out port)) return;

                        // 3. 通过 BouncyCastle 做 TLS 1.2 连接
                        using (TcpClient targetClient = new TcpClient())
                        {
                            targetClient.ReceiveTimeout = 30000;
                            targetClient.SendTimeout = 30000;
                            targetClient.Connect(host, port);

                            TlsClientProtocol tlsProtocol = new TlsClientProtocol(targetClient.GetStream(), new Org.BouncyCastle.Security.SecureRandom());
                            SimpleTlsClient tlsClient = new SimpleTlsClient(host);
                            tlsProtocol.Connect(tlsClient);

                            // 4. 转发请求 → 目标（修复 Host 头 + 追加 Connection: close）
                            string targetHostPort = host + ":" + port;
                            string modifiedRequest = RewriteHostHeader(request, targetHostPort);
                            modifiedRequest = AddConnectionClose(modifiedRequest);
                            byte[] requestBytes = Encoding.UTF8.GetBytes(modifiedRequest);
                            tlsProtocol.Stream.Write(requestBytes, 0, requestBytes.Length);
                            tlsProtocol.Stream.Flush();

                            // 5. 转发响应 → 客户端（逐 chunk 立即 flush 以支持 SSE 流式输出）
                            byte[] buffer = new byte[8192];
                            int bytesRead;
                            while ((bytesRead = tlsProtocol.Stream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                clientStream.Write(buffer, 0, bytesRead);
                                clientStream.Flush();
                            }
                        }
                    }
                }
            }
            catch (Org.BouncyCastle.Crypto.Tls.TlsNoCloseNotifyException)
            {
                // 服务器未发送 close_notify 就关闭连接，常见于某些反向代理，忽略
            }
            catch (System.IO.IOException ex)
            {
                _logger.Log(string.Format("[TlsProxy] HandleClient IOException: {0} - {1}", ex.GetType().Name, ex.Message), "ERROR");
                // 连接超时或重置，通常已被上层重试恢复
            }
            catch (Exception ex)
            {
                _logger.Log(string.Format("[TlsProxy] HandleClient error: {0} - {1}", ex.GetType().Name, ex.Message), "ERROR");
                if (ex.InnerException != null)
                {
                    _logger.Log(string.Format("[TlsProxy] Inner: {0} - {1}", ex.InnerException.GetType().Name, ex.InnerException.Message), "ERROR");
                }
            }
        }

        /// <summary>从 NetworkStream 读取完整 HTTP 请求（含 body），使用缓冲读取</summary>
        private static string ReadHttpRequest(NetworkStream stream)
        {
            byte[] buffer = new byte[65536];
            int totalRead = 0;
            int headerEnd = -1;
            int contentLength = 0;

            // 缓冲读取直到找到 \r\n\r\n 头结束标记
            while (headerEnd < 0)
            {
                if (totalRead >= buffer.Length)
                {
                    // 扩容缓冲区
                    byte[] newBuffer = new byte[buffer.Length * 2];
                    Array.Copy(buffer, newBuffer, buffer.Length);
                    buffer = newBuffer;
                }
                int read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                if (read <= 0) return null;
                totalRead += read;
                string text = Encoding.ASCII.GetString(buffer, 0, totalRead);
                headerEnd = text.IndexOf("\r\n\r\n");
            }

            string headerText = Encoding.ASCII.GetString(buffer, 0, headerEnd + 4);
            int bodyStart = headerEnd + 4;

            // 解析 Content-Length
            string[] lines = headerText.Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    string val = trimmed.Substring(15).Trim();
                    int.TryParse(val, out contentLength);
                    break;
                }
            }

            // 读 body（如有）
            if (contentLength > 0)
            {
                int alreadyInBuffer = totalRead - bodyStart;
                if (alreadyInBuffer < 0) alreadyInBuffer = 0;
                if (alreadyInBuffer > contentLength) alreadyInBuffer = contentLength;

                byte[] body = new byte[contentLength];
                if (alreadyInBuffer > 0)
                {
                    Array.Copy(buffer, bodyStart, body, 0, alreadyInBuffer);
                }

                int bodyOffset = alreadyInBuffer;
                int bodyLeft = contentLength - alreadyInBuffer;
                while (bodyLeft > 0)
                {
                    int read = stream.Read(body, bodyOffset, bodyLeft);
                    if (read <= 0) break;
                    bodyOffset += read;
                    bodyLeft -= read;
                }
                return headerText + Encoding.UTF8.GetString(body, 0, bodyOffset);
            }

            return headerText;
        }

        /// <summary>从 HTTP 请求中提取目标 host:port（优先读取 X-Target-Host 自定义头）</summary>
        private static bool ParseHost(string httpRequest, out string host, out int port)
        {
            host = null;
            port = 443;

            // 优先读取 X-Target-Host（SDK 注入的原始目标）
            string xTargetHost = null;
            string[] lines = httpRequest.Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("X-Target-Host:", StringComparison.OrdinalIgnoreCase))
                {
                    xTargetHost = trimmed.Substring(14).Trim();
                    break;
                }
            }

            // 其次读取 Host 头
            if (string.IsNullOrEmpty(xTargetHost))
            {
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                    {
                        xTargetHost = trimmed.Substring(5).Trim();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(xTargetHost)) return false;

            int colonIndex = xTargetHost.LastIndexOf(':');
            if (colonIndex > 0)
            {
                host = xTargetHost.Substring(0, colonIndex);
                int.TryParse(xTargetHost.Substring(colonIndex + 1), out port);
            }
            else
            {
                host = xTargetHost;
            }
            return true;
        }

        /// <summary>替换 HTTP 请求中的 Host 头为目标地址</summary>
        private static string RewriteHostHeader(string httpRequest, string targetHostPort)
        {
            // 查找并替换 Host: 头
            int hostIdx = -1;
            string[] lines = httpRequest.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                {
                    hostIdx = i;
                    break;
                }
            }
            if (hostIdx < 0) return httpRequest;

            string oldHostLine = lines[hostIdx];
            string newHostLine = "Host: " + targetHostPort + (oldHostLine.EndsWith("\r") ? "\r" : "");
            lines[hostIdx] = newHostLine;
            string newHeaders = string.Join("\n", lines);
            int bodySep = httpRequest.IndexOf("\r\n\r\n");
            return newHeaders + (bodySep >= 0 ? httpRequest.Substring(bodySep) : "");
        }

        /// <summary>替换或追加 Connection: close 头</summary>
        private static string AddConnectionClose(string httpRequest)
        {
            int headerEnd = httpRequest.IndexOf("\r\n\r\n");
            if (headerEnd < 0) return httpRequest;

            // 移除已有的 Connection 头
            string headers = httpRequest.Substring(0, headerEnd);
            int connIdx = headers.IndexOf("\r\nConnection:", StringComparison.OrdinalIgnoreCase);
            if (connIdx < 0)
                connIdx = headers.IndexOf("\nConnection:", StringComparison.OrdinalIgnoreCase);
            
            if (connIdx >= 0)
            {
                int connEnd = headers.IndexOf("\r\n", connIdx + 2);
                if (connEnd < 0) connEnd = headers.Length;
                headers = headers.Substring(0, connIdx) + headers.Substring(connEnd);
            }

            return headers.TrimEnd('\r', '\n') + "\r\nConnection: close\r\n\r\n" + httpRequest.Substring(headerEnd + 4);
        }

        public void Dispose()
        {
            Stop();
        }
    }

    /// <summary>
    /// BouncyCastle 最小 TLS 客户端实现，仅支持 TLS 1.2，不做证书校验。
    /// </summary>
    internal class SimpleTlsClient : DefaultTlsClient
    {
        private readonly string _host;

        /// <summary>
        /// 现代 TLS 1.2 密码套件，ECDHE 优先。
        /// </summary>
        private static readonly int[] _cipherSuites = new int[]
        {
            CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
            CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
            CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,
            CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384,
            CipherSuite.TLS_RSA_WITH_AES_128_GCM_SHA256,
            CipherSuite.TLS_RSA_WITH_AES_256_GCM_SHA384
        };

        public SimpleTlsClient(string host)
        {
            _host = host;
        }

        public override ProtocolVersion MinimumVersion
        {
            get { return ProtocolVersion.TLSv12; }
        }

        public override int[] GetCipherSuites()
        {
            return _cipherSuites;
        }

        public override IDictionary GetClientExtensions()
        {
            // 手动构造 SNI (Server Name Indication) 扩展字节。
            // TLS 格式: ExtensionType=0(2字节) | ListLength(2字节) | NameType=0(1字节) | NameLength(2字节) | Name(N字节)
            // BC 1.8.9 的 ServerNameList/ServerName API 与后续版本不兼容，手动编码最可靠。
            IDictionary extensions = new Hashtable();
            byte[] nameBytes = Encoding.UTF8.GetBytes(_host);
            int sniLen = 1 + 2 + nameBytes.Length;       // nameType(1) + nameLen(2) + name(N)
            byte[] sniData = new byte[2 + sniLen];        // listLen(2) + sniEntry
            sniData[0] = (byte)(sniLen >> 8);             // ServerNameList length (big-endian)
            sniData[1] = (byte)sniLen;
            sniData[2] = (byte)0;                         // NameType: 0 = host_name
            sniData[3] = (byte)(nameBytes.Length >> 8);   // Name length (big-endian)
            sniData[4] = (byte)nameBytes.Length;
            Array.Copy(nameBytes, 0, sniData, 5, nameBytes.Length);
            extensions.Add(0, sniData);                   // ExtensionType 0 = server_name
            return extensions;
        }

        public override TlsAuthentication GetAuthentication()
        {
            return new SimpleTlsAuthentication(_host);
        }
    }

    /// <summary>
    /// TLS 认证实现：验证证书有效期和主机名，接受有效证书。
    /// 不做系统信任链校验（XP 系统根证书可能过期），但记录警告。
    /// </summary>
    internal class SimpleTlsAuthentication : TlsAuthentication
    {
        private readonly string _host;
        private static readonly ILogger _logger = new FileLogger();

        public SimpleTlsAuthentication(string host)
        {
            _host = host;
        }

        public void NotifyServerCertificate(Certificate serverCertificate)
        {
            if (serverCertificate == null || serverCertificate.IsEmpty)
            {
                return;
            }

            try
            {
                // 验证证书有效期
                Org.BouncyCastle.Asn1.X509.X509CertificateStructure cert =
                    serverCertificate.GetCertificateAt(0);
                DateTime now = DateTime.UtcNow;
                if (now < cert.StartDate.ToDateTime() || now > cert.EndDate.ToDateTime())
                {
                    _logger.Log(string.Format("[TlsProxy] Certificate expired or not yet valid for {0}", _host), "WARN");
                }

                // 验证主机名（CN 或 SAN）
                if (!string.IsNullOrEmpty(_host))
                {
                    bool hostMatch = false;
                    string cn = ExtractCN(cert.Subject);
                    if (!string.IsNullOrEmpty(cn) &&
                        string.Equals(cn, _host, StringComparison.OrdinalIgnoreCase))
                    {
                        hostMatch = true;
                    }
                    if (!hostMatch)
                    {
                        // 检查 SAN (Subject Alternative Names)
                        hostMatch = CheckSAN(cert, _host);
                    }
                    if (!hostMatch)
                    {
                        _logger.Log(string.Format("[TlsProxy] Hostname mismatch: cert CN={0} expected={1}", cn ?? "?", _host), "WARN");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log(string.Format("[TlsProxy] Certificate validation error: {0}", ex.Message), "ERROR");
            }
        }

        private static string ExtractCN(Org.BouncyCastle.Asn1.X509.X509Name subject)
        {
            try
            {
                var cnOid = Org.BouncyCastle.Asn1.X509.X509Name.CN;
                var values = subject.GetValueList(cnOid);
                if (values != null && values.Count > 0)
                {
                    return values[0].ToString();
                }
            }
            catch { }
            return null;
        }

        private static bool CheckSAN(Org.BouncyCastle.Asn1.X509.X509CertificateStructure cert, string host)
        {
            try
            {
                var extensions = cert.TbsCertificate.Extensions;
                if (extensions == null) return false;

                foreach (object extObj in extensions.GetExtensionOids())
                {
                    var oid = (Org.BouncyCastle.Asn1.DerObjectIdentifier)extObj;
                    if (oid.Id.Equals("2.5.29.17")) // subjectAltName OID
                    {
                        var ext = extensions.GetExtension(oid);
                        if (ext != null)
                        {
                            var octets = ext.Value.GetOctets();
                            var asn1 = Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(octets);
                            var sanSeq = Org.BouncyCastle.Asn1.Asn1Sequence.GetInstance(asn1);
                            foreach (var entry in sanSeq)
                            {
                                var tagged = entry as Org.BouncyCastle.Asn1.DerTaggedObject;
                                if (tagged != null)
                                {
                                    // Tag 2 = dNSName
                                    if (tagged.TagNo == 2)
                                    {
                                        string dnsName = tagged.GetObject().ToString();
                                        if (string.Equals(dnsName, host, StringComparison.OrdinalIgnoreCase))
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public TlsCredentials GetClientCredentials(CertificateRequest certificateRequest)
        {
            return null;
        }
    }
}
