using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace UloManager.Core;

/// <summary>
/// Where downloaded files are placed: a local folder, a UNC share (optionally with its own
/// credentials) or an FTP server, each with optional retention, so scheduled synchronisation does
/// not need a second tool.
/// </summary>
public abstract class UloDestination : IDisposable
{
    /// <summary>Human readable description, used in log output.</summary>
    public abstract string Description { get; }

    /// <summary>
    /// Builds a destination from a path. `ftp://host/path` selects FTP, a `\\server\share` path or a
    /// mounted path selects the share/local handler.
    /// </summary>
    public static UloDestination Create(string path, string? userName = null, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A destination path is required.", nameof(path));
        }

        if (path.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase))
        {
            return new UloFtpDestination(path, userName, password);
        }

        return new UloFolderDestination(path, userName, password);
    }

    /// <summary>Connects to the destination if it needs it. Call before any transfer.</summary>
    public virtual Task PrepareAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>True when the file is already present, so it does not have to be downloaded again.</summary>
    public abstract Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default);

    /// <summary>Stores a local file at the given relative path, replacing anything already there.</summary>
    public abstract Task StoreAsync(string localFile, string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Removes files older than the retention period. Returns how many were deleted;
    /// a retention of zero or less keeps everything.
    /// </summary>
    public abstract Task<int> ApplyRetentionAsync(TimeSpan retention, CancellationToken ct = default);

    /// <summary>A working folder the caller may download into before storing.</summary>
    public virtual string CreateStagingFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "UloManager", Path.GetRandomFileName());
        Directory.CreateDirectory(folder);
        return folder;
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// A local folder or a network share. On Windows a share can be connected with explicit credentials,
/// which is what the original library used `WNetAddConnection2` for; elsewhere the share is expected
/// to be mounted by the operating system already.
/// </summary>
public sealed class UloFolderDestination : UloDestination
{
    private readonly string _root;
    private readonly string? _userName;
    private readonly string? _password;
    private bool _connected;

    public UloFolderDestination(string root, string? userName = null, string? password = null)
    {
        _root = root.TrimEnd('/', '\\');
        _userName = userName;
        _password = password;
    }

    public override string Description => _root;

    public override Task PrepareAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(_userName) && _root.StartsWith(@"\\", StringComparison.Ordinal))
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException(
                    "Credentials for a network share can only be supplied on Windows. " +
                    "Mount the share first and pass the mount point as the destination.");
            }

            Connect();
        }

        Directory.CreateDirectory(_root);
        return Task.CompletedTask;
    }

    public override Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default)
        => Task.FromResult(File.Exists(Path.Combine(_root, relativePath)));

    public override Task StoreAsync(string localFile, string relativePath, CancellationToken ct = default)
    {
        var target = Path.Combine(_root, relativePath);

        // Downloads go straight into this folder (CreateStagingFolder returns the root), so for a
        // local or share destination the file is already exactly where it belongs.
        if (string.Equals(
                Path.GetFullPath(localFile),
                Path.GetFullPath(target),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var folder = Path.GetDirectoryName(target);

        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.Copy(localFile, target, overwrite: true);
        return Task.CompletedTask;
    }

    public override Task<int> ApplyRetentionAsync(TimeSpan retention, CancellationToken ct = default)
        => Task.FromResult(UloMediaService.ApplyRetention(_root, retention));

    /// <summary>Files can be downloaded straight into the destination, no staging needed.</summary>
    public override string CreateStagingFolder() => _root;

    [SupportedOSPlatform("windows")]
    private void Connect()
    {
        var resource = new NetResource
        {
            Scope = 2,          // RESOURCE_GLOBALNET
            ResourceType = 1,   // RESOURCETYPE_DISK
            DisplayType = 3,    // RESOURCEDISPLAYTYPE_SHARE
            Usage = 0,
            RemoteName = _root,
        };

        var result = WNetAddConnection2(resource, _password, _userName, 0);

        // 1219 = a connection with different credentials already exists, which is fine for our purposes.
        if (result is not 0 and not 1219)
        {
            throw new IOException($"Could not connect to '{_root}' as '{_userName}' (error {result}).");
        }

        _connected = result == 0;
    }

    public override void Dispose()
    {
        if (_connected && OperatingSystem.IsWindows())
        {
            WNetCancelConnection2(_root, 0, true);
            _connected = false;
        }

        base.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private sealed class NetResource
    {
        public int Scope;
        public int ResourceType;
        public int DisplayType;
        public int Usage;
        public string? LocalName;
        public string? RemoteName;
        public string? Comment;
        public string? Provider;
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetAddConnection2(NetResource netResource, string? password, string? username, int flags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetCancelConnection2(string name, int flags, bool force);
}

/// <summary>
/// An FTP server. Files are downloaded locally first and then uploaded, because the camera's HTTP
/// server and the FTP server have nothing in common.
/// </summary>
public sealed class UloFtpDestination : UloDestination
{
    private readonly Uri _root;
    private readonly NetworkCredential _credential;
    private string? _staging;

    public UloFtpDestination(string root, string? userName, string? password)
    {
        _root = new Uri(root.TrimEnd('/') + "/");
        _credential = string.IsNullOrEmpty(userName)
            ? new NetworkCredential("anonymous", "anonymous@")
            : new NetworkCredential(userName, password ?? string.Empty);
    }

    public override string Description => _root.ToString();

    public override async Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default)
    {
        try
        {
            var request = CreateRequest(relativePath, WebRequestMethods.Ftp.GetFileSize);
            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            return response.ContentLength >= 0;
        }
        catch (WebException)
        {
            return false;
        }
    }

    public override async Task StoreAsync(string localFile, string relativePath, CancellationToken ct = default)
    {
        await EnsureFoldersAsync(relativePath).ConfigureAwait(false);

        var request = CreateRequest(relativePath, WebRequestMethods.Ftp.UploadFile);

        await using (var stream = await request.GetRequestStreamAsync().ConfigureAwait(false))
        await using (var source = File.OpenRead(localFile))
        {
            await source.CopyToAsync(stream, ct).ConfigureAwait(false);
        }

        using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);

        if (response.StatusCode is not (FtpStatusCode.ClosingData or FtpStatusCode.FileActionOK or FtpStatusCode.CommandOK))
        {
            throw new IOException($"FTP upload of '{relativePath}' failed: {response.StatusDescription?.Trim()}");
        }
    }

    public override async Task<int> ApplyRetentionAsync(TimeSpan retention, CancellationToken ct = default)
    {
        if (retention <= TimeSpan.Zero)
        {
            return 0;
        }

        var limit = DateTime.Now - retention;
        var removed = 0;

        foreach (var name in await ListAsync(string.Empty).ConfigureAwait(false))
        {
            var stamp = UloMediaService.ParseTimestamp(name);
            if (stamp is null || stamp >= limit)
            {
                continue;
            }

            try
            {
                var request = CreateRequest(name, WebRequestMethods.Ftp.DeleteFile);
                using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
                removed++;
            }
            catch (WebException)
            {
                // Locked or already gone - try again next run.
            }
        }

        return removed;
    }

    public override string CreateStagingFolder()
    {
        _staging ??= base.CreateStagingFolder();
        return _staging;
    }

    private async Task<IReadOnlyList<string>> ListAsync(string relativeFolder)
    {
        try
        {
            var request = CreateRequest(relativeFolder, WebRequestMethods.Ftp.ListDirectory);
            using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            await using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);

            var names = new List<string>();
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.Length > 0)
                {
                    names.Add(line.Trim());
                }
            }

            return names;
        }
        catch (WebException)
        {
            return Array.Empty<string>();
        }
    }

    private async Task EnsureFoldersAsync(string relativePath)
    {
        var parts = relativePath.Replace('\\', '/').Split('/');
        if (parts.Length < 2)
        {
            return;
        }

        var current = string.Empty;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            current = current.Length == 0 ? parts[i] : current + "/" + parts[i];

            try
            {
                var request = CreateRequest(current, WebRequestMethods.Ftp.MakeDirectory);
                using var response = (FtpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
            }
            catch (WebException)
            {
                // Already exists.
            }
        }
    }

    private FtpWebRequest CreateRequest(string relativePath, string method)
    {
        var uri = new Uri(_root, relativePath.Replace('\\', '/'));

#pragma warning disable SYSLIB0014 // FtpWebRequest is obsolete but remains the only FTP client in the BCL.
        var request = (FtpWebRequest)WebRequest.Create(uri);
#pragma warning restore SYSLIB0014

        request.Method = method;
        request.Credentials = _credential;
        request.UseBinary = true;
        request.UsePassive = true;
        request.KeepAlive = false;
        return request;
    }

    public override void Dispose()
    {
        if (_staging is not null && Directory.Exists(_staging))
        {
            try
            {
                Directory.Delete(_staging, recursive: true);
            }
            catch (IOException)
            {
                // Temporary files will be cleaned by the OS.
            }
        }

        base.Dispose();
    }
}
