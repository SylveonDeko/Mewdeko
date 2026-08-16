using System.IO;

namespace Mewdeko.Services.Impl;

/// <summary>
///     Writes assets into the directory the bot's CDN serves, so they get a publicly fetchable URL.
///     Discord fetches images like webhook avatars by URL, so anything the dashboard uploads has to be
///     given a real address before Discord can use it.
/// </summary>
public class CdnStorageService(IBotCredentials creds, ILogger<CdnStorageService> logger) : INService
{
    private bool? usable;

    /// <summary>
    ///     Whether this instance has a CDN that can actually be written to. A path being configured is not
    ///     enough: the default points at an nginx directory that does not exist on a developer machine and
    ///     is not writable inside most containers, so the directory is probed once and the answer cached.
    ///     When this is false the caller falls back to serving the asset through the dashboard.
    /// </summary>
    public bool IsConfigured
    {
        get
        {
            if (usable.HasValue)
                return usable.Value;

            if (string.IsNullOrWhiteSpace(creds.CdnPath) || string.IsNullOrWhiteSpace(creds.CdnUrl))
                return (usable = false).Value;

            try
            {
                Directory.CreateDirectory(creds.CdnPath);
                var probe = Path.Combine(creds.CdnPath, $".mewdeko-write-test-{Guid.NewGuid():N}");
                File.WriteAllBytes(probe, []);
                File.Delete(probe);
                usable = true;
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex,
                    "CDN path {CdnPath} is not writable, so uploads will be served through the dashboard instead",
                    creds.CdnPath);
                usable = false;
            }

            return usable.Value;
        }
    }

    /// <summary>
    ///     Writes bytes into a folder of the CDN and returns the public URL they are served under.
    ///     Overwrites any existing file with the same name.
    /// </summary>
    /// <param name="folder">The CDN subfolder, for example <c>personas</c>.</param>
    /// <param name="fileName">The file name including its extension.</param>
    /// <param name="content">The bytes to write.</param>
    /// <returns>The public URL, or null when no CDN is configured or the write failed.</returns>
    public async Task<string?> SaveAsync(string folder, string fileName, byte[] content)
    {
        if (!IsConfigured)
            return null;

        try
        {
            var directory = Path.Combine(creds.CdnPath, folder);
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, fileName), content);
            return $"{creds.CdnUrl}/{folder}/{fileName}";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write {FileName} to the CDN folder {Folder}", fileName, folder);
            return null;
        }
    }

    /// <summary>
    ///     Removes a file from the CDN. Missing files are ignored.
    /// </summary>
    /// <param name="folder">The CDN subfolder the file lives in.</param>
    /// <param name="fileName">The file name including its extension.</param>
    public Task DeleteAsync(string folder, string fileName)
    {
        if (!IsConfigured)
            return Task.CompletedTask;

        try
        {
            var path = Path.Combine(creds.CdnPath, folder, fileName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete {FileName} from the CDN folder {Folder}", fileName, folder);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Removes every file in a CDN folder whose name starts with the given prefix, used to clear the
    ///     older versions of an asset that is replaced rather than edited in place.
    /// </summary>
    /// <param name="folder">The CDN subfolder to clean.</param>
    /// <param name="prefix">The file name prefix to match.</param>
    public Task DeleteByPrefixAsync(string folder, string prefix)
    {
        if (!IsConfigured)
            return Task.CompletedTask;

        try
        {
            var directory = Path.Combine(creds.CdnPath, folder);
            if (!Directory.Exists(directory))
                return Task.CompletedTask;

            foreach (var path in Directory.EnumerateFiles(directory, $"{prefix}*"))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear {Prefix}* from the CDN folder {Folder}", prefix, folder);
        }

        return Task.CompletedTask;
    }
}