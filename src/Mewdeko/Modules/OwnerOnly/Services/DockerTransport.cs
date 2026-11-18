using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Mewdeko.Modules.OwnerOnly.Services;

/// <summary>
///     How a Docker host address is reached.
/// </summary>
public enum DockerEndpointKind
{
    /// <summary>
    ///     A unix domain socket.
    /// </summary>
    Unix,

    /// <summary>
    ///     Plain HTTP over tcp.
    /// </summary>
    Tcp,

    /// <summary>
    ///     HTTPS over tcp with a client certificate.
    /// </summary>
    TcpTls
}

/// <summary>
///     A parsed Docker host address.
/// </summary>
/// <param name="Kind">How the host is reached.</param>
/// <param name="Address">The socket path for unix hosts, the base URL for tcp hosts.</param>
/// <param name="Problem">Why the address cannot be used, or null when it can.</param>
public sealed record DockerEndpoint(DockerEndpointKind Kind, string Address, string? Problem);

/// <summary>
///     The client certificate and certificate authority used for a tcp Docker host.
/// </summary>
/// <param name="CertFile">The PFX file holding the client certificate.</param>
/// <param name="CertPassword">The PFX password, or empty.</param>
/// <param name="CaFile">The PEM file of the authority the server certificate must chain to.</param>
/// <param name="CertPem">The client certificate as PEM text. Used instead of the PFX file when set with the key.</param>
/// <param name="KeyPem">The client certificate's private key as PEM text.</param>
/// <param name="CaPem">The authority certificate as PEM text. Used instead of the CA file when set.</param>
public sealed record DockerTlsSettings(string CertFile, string CertPassword, string CaFile, string CertPem = "",
    string KeyPem = "", string CaPem = "")
{
    /// <summary>
    ///     Gets whether the client certificate and its key were given as PEM text.
    /// </summary>
    public bool HasInlineClientCertificate
    {
        get
        {
            return !string.IsNullOrWhiteSpace(CertPem) && !string.IsNullOrWhiteSpace(KeyPem);
        }
    }

    /// <summary>
    ///     Gets whether both the client certificate and the authority are set, as files or as PEM text.
    /// </summary>
    public bool IsConfigured
    {
        get
        {
            return (HasInlineClientCertificate || !string.IsNullOrWhiteSpace(CertFile)) &&
                   (!string.IsNullOrWhiteSpace(CaPem) || !string.IsNullOrWhiteSpace(CaFile));
        }
    }
}

/// <summary>
///     Builds the HTTP clients used to talk to a Docker daemon.
/// </summary>
public static class DockerTransport
{
    /// <summary>
    ///     Works out how an address is reached. Plain tcp is only accepted for loopback addresses unless TLS is
    ///     configured.
    /// </summary>
    /// <param name="host">The unix:// or tcp:// address.</param>
    /// <param name="tlsConfigured">Whether a client certificate and authority are set.</param>
    public static DockerEndpoint ParseEndpoint(string host, bool tlsConfigured)
    {
        host = host.Trim();
        if (host.StartsWith("unix://", StringComparison.OrdinalIgnoreCase))
        {
            var path = host["unix://".Length..];
            return string.IsNullOrWhiteSpace(path)
                ? new DockerEndpoint(DockerEndpointKind.Unix, "", "The unix address has no socket path")
                : new DockerEndpoint(DockerEndpointKind.Unix, path, null);
        }

        if (!host.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
            return new DockerEndpoint(DockerEndpointKind.Tcp, "", $"'{host}' is not a unix:// or tcp:// address");

        var authority = host["tcp://".Length..].TrimEnd('/');
        if (!Uri.TryCreate("http://" + authority, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host) ||
            uri.PathAndQuery != "/")
            return new DockerEndpoint(DockerEndpointKind.Tcp, "", $"'{host}' is not a valid tcp address");

        if (tlsConfigured)
            return new DockerEndpoint(DockerEndpointKind.TcpTls, $"https://{uri.Authority}/", null);

        if (!IsLoopbackHost(uri.Host))
            return new DockerEndpoint(DockerEndpointKind.Tcp, "",
                "Plain tcp is only allowed for loopback addresses, set a client certificate and CA file for remote hosts");

        return new DockerEndpoint(DockerEndpointKind.Tcp, $"http://{uri.Authority}/", null);
    }

    /// <summary>
    ///     Checks whether a host name or address is the local machine.
    /// </summary>
    /// <param name="host">The host part of an address.</param>
    public static bool IsLoopbackHost(string host)
    {
        host = host.Trim('[', ']');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    /// <summary>
    ///     Builds a client for a unix domain socket.
    /// </summary>
    /// <param name="socketPath">The socket path.</param>
    public static (HttpClient, string, string, string?) BuildUnix(string socketPath)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);
                    return new NetworkStream(socket, true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };

        return (new HttpClient(handler)
        {
            BaseAddress = new Uri("http://docker/"), Timeout = Timeout.InfiniteTimeSpan
        }, "unix://" + socketPath, socketPath, null);
    }

    /// <summary>
    ///     Builds a client for an explicitly configured host.
    /// </summary>
    /// <param name="host">The unix:// or tcp:// address.</param>
    /// <param name="tls">The TLS settings for tcp hosts.</param>
    public static (HttpClient? Client, string? Endpoint, string? SocketPath, string? Problem) Create(string host,
        DockerTlsSettings? tls)
    {
        var endpoint = ParseEndpoint(host, tls is { IsConfigured: true });
        if (endpoint.Problem != null)
            return (null, host, null, endpoint.Problem);

        switch (endpoint.Kind)
        {
            case DockerEndpointKind.Unix:
                return BuildUnix(endpoint.Address);
            case DockerEndpointKind.Tcp:
                return (new HttpClient
                {
                    BaseAddress = new Uri(endpoint.Address), Timeout = Timeout.InfiniteTimeSpan
                }, host, null, null);
            default:
                try
                {
                    return (BuildTls(endpoint.Address, tls!), host, null, null);
                }
                catch (Exception ex) when (ex is IOException or System.Security.Cryptography.CryptographicException
                                               or UnauthorizedAccessException)
                {
                    return (null, host, null, $"The TLS files could not be read: {ex.Message}");
                }
        }
    }

    /// <summary>
    ///     Decides whether a server certificate is acceptable: the name must match and the chain must end at one
    ///     of the given authorities, whatever the system trusts.
    /// </summary>
    /// <param name="certificate">The certificate the server presented.</param>
    /// <param name="errors">The errors the platform found.</param>
    /// <param name="authorities">The accepted certificate authorities.</param>
    public static bool ValidateServerCertificate(X509Certificate2? certificate, SslPolicyErrors errors,
        X509Certificate2Collection authorities)
    {
        if (certificate == null || errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch) ||
            errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
            return false;

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(authorities);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(certificate);
    }

    /// <summary>
    ///     Loads the client certificate and its key, from inline PEM when given and from the PFX file otherwise.
    /// </summary>
    /// <param name="tls">The TLS settings.</param>
    public static X509Certificate2 LoadClientCertificate(DockerTlsSettings tls)
    {
        if (tls.HasInlineClientCertificate)
        {
            using var pem = X509Certificate2.CreateFromPem(tls.CertPem, tls.KeyPem);
            return X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pfx), null);
        }

        var password = string.IsNullOrEmpty(tls.CertPassword) ? null : tls.CertPassword;
        return X509CertificateLoader.LoadPkcs12FromFile(tls.CertFile, password);
    }

    /// <summary>
    ///     Loads the accepted certificate authorities, from inline PEM when given and from the PEM file otherwise.
    /// </summary>
    /// <param name="tls">The TLS settings.</param>
    public static X509Certificate2Collection LoadAuthorities(DockerTlsSettings tls)
    {
        var authorities = new X509Certificate2Collection();
        if (!string.IsNullOrWhiteSpace(tls.CaPem))
            authorities.ImportFromPem(tls.CaPem);
        else
            authorities.ImportFromPemFile(tls.CaFile);

        if (authorities.Count == 0)
            throw new IOException("The CA holds no certificates");

        return authorities;
    }

    /// <summary>
    ///     Reads the TLS material as inline PEM text, so it can be handed to a container that cannot see the host's
    ///     files. Inline values are passed through and file values are converted.
    /// </summary>
    /// <param name="tls">The TLS settings.</param>
    /// <returns>The certificate, key and CA PEM, or null when the settings are incomplete or unreadable.</returns>
    public static (string CertPem, string KeyPem, string CaPem)? ExportPem(DockerTlsSettings tls)
    {
        if (!tls.IsConfigured)
            return null;

        try
        {
            string certPem;
            string keyPem;
            if (tls.HasInlineClientCertificate)
            {
                certPem = tls.CertPem;
                keyPem = tls.KeyPem;
            }
            else
            {
                var password = string.IsNullOrEmpty(tls.CertPassword) ? null : tls.CertPassword;
                using var pfx = X509CertificateLoader.LoadPkcs12FromFile(tls.CertFile, password,
                    X509KeyStorageFlags.Exportable);
                certPem = pfx.ExportCertificatePem();
                using var rsa = pfx.GetRSAPrivateKey();
                using var ecdsa = rsa == null ? pfx.GetECDsaPrivateKey() : null;
                if (rsa != null)
                    keyPem = rsa.ExportPkcs8PrivateKeyPem();
                else if (ecdsa != null)
                    keyPem = ecdsa.ExportPkcs8PrivateKeyPem();
                else
                    return null;
            }

            var caPem = !string.IsNullOrWhiteSpace(tls.CaPem) ? tls.CaPem : File.ReadAllText(tls.CaFile);
            return (certPem, keyPem, caPem);
        }
        catch (Exception ex) when (ex is IOException or System.Security.Cryptography.CryptographicException
                                       or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static HttpClient BuildTls(string baseAddress, DockerTlsSettings tls)
    {
        var clientCertificate = LoadClientCertificate(tls);
        var authorities = LoadAuthorities(tls);

        var handler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                ClientCertificates = [clientCertificate],
                RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
                    ValidateServerCertificate(certificate as X509Certificate2 ??
                                              (certificate == null ? null : new X509Certificate2(certificate)), errors,
                        authorities)
            }
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri(baseAddress), Timeout = Timeout.InfiniteTimeSpan
        };
    }
}
