using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace PlayniteDlssgPlugin
{
    /// <summary>
    /// Holds the optional dlssg_for_sm86 payload.
    ///
    /// Since 0.4.0 upstream ships a single 0.85 MB proxy plus a dlssg_sm86 folder;
    /// most of the folder is optional (the XeSS model pack, the super-resolution
    /// pair, the five alternative proxies). Only the files every user needs are
    /// packed into the .pext - everything else is downloaded on first use into
    /// %LOCALAPPDATA%\PlayniteDlssgPlugin\components\&lt;upstream version&gt; and reused
    /// from there, so an update to a new upstream version never mixes payloads.
    /// </summary>
    internal static class ComponentCache
    {
        /// Upstream release this plugin's bundled payload and URLs are pinned to.
        public const string UpstreamVersion = "0.4.2";

        private const string UpstreamRepo = "sdli1995/dlssg_for_sm86";

        /// Files that exist in the upstream git repository and can be fetched one by one.
        private static readonly string RawBaseUrl =
            "https://raw.githubusercontent.com/" + UpstreamRepo + "/" + UpstreamVersion + "/";

        /// Files that are only shipped inside the release archive (the super-resolution pair).
        private static readonly string ReleaseZipUrl =
            "https://github.com/" + UpstreamRepo + "/releases/download/" + UpstreamVersion +
            "/dlssg-release-" + UpstreamVersion + "-x64.zip";

        /// <summary>Optional files served from the repository (small, fetched individually).</summary>
        public static readonly string[] RawFiles =
        {
            @"alternatives\winmm.dll",
            @"alternatives\dbghelp.dll",
            @"alternatives\dinput8.dll",
            @"alternatives\dxgi.dll",
            @"alternatives\d3d12.dll",
            @"dlssg_sm86\xefg\xefg_kernels.dll"
        };

        /// <summary>Optional files that only exist inside the release zip.</summary>
        public static readonly string[] ZipOnlyFiles =
        {
            @"dlssg_sm86\sr\nvngx_dlss.dll",
            @"dlssg_sm86\sr\sr_kernels.dll"
        };

        /// <summary>Approximate on-disk size per optional file, used for the download prompt.</summary>
        private static readonly Dictionary<string, long> SizeHints = new Dictionary<string, long>(
            StringComparer.OrdinalIgnoreCase)
        {
            { @"alternatives\winmm.dll", 1L * 1024 * 1024 },
            { @"alternatives\dbghelp.dll", 1L * 1024 * 1024 },
            { @"alternatives\dinput8.dll", 1L * 1024 * 1024 },
            { @"alternatives\dxgi.dll", 1L * 1024 * 1024 },
            { @"alternatives\d3d12.dll", 1L * 1024 * 1024 },
            { @"dlssg_sm86\xefg\xefg_kernels.dll", 17L * 1024 * 1024 },
            { @"dlssg_sm86\sr\nvngx_dlss.dll", 75L * 1024 * 1024 },
            { @"dlssg_sm86\sr\sr_kernels.dll", 107L * 1024 * 1024 }
        };

        public static string CacheRoot
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PlayniteDlssgPlugin",
                    "components",
                    UpstreamVersion);
            }
        }

        public static string GetCachedPath(string relativePath)
        {
            return Path.Combine(CacheRoot, relativePath);
        }

        public static bool IsCached(string relativePath)
        {
            var path = GetCachedPath(relativePath);
            try
            {
                return File.Exists(path) && new FileInfo(path).Length > 0;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static bool IsZipOnly(string relativePath)
        {
            return ZipOnlyFiles.Any(file => string.Equals(file, relativePath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Rough download size for a set of files, in bytes.</summary>
        public static long EstimateDownloadSize(IEnumerable<string> relativePaths)
        {
            long total = 0;
            var needsZip = false;
            foreach (var relativePath in relativePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (IsZipOnly(relativePath))
                {
                    needsZip = true;
                    continue;
                }

                long size;
                total += SizeHints.TryGetValue(relativePath, out size) ? size : 1024 * 1024;
            }

            // The release archive carries the super-resolution pair plus everything
            // else; it is ~90 MB on disk and replaces the per-file downloads it covers.
            if (needsZip)
            {
                total = Math.Max(total, 90L * 1024 * 1024);
            }

            return total;
        }

        /// <summary>Total size currently held in the cache, in bytes.</summary>
        public static long GetCacheSize()
        {
            try
            {
                var root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PlayniteDlssgPlugin");
                if (!Directory.Exists(root))
                {
                    return 0;
                }

                return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                    .Sum(file =>
                    {
                        try
                        {
                            return new FileInfo(file).Length;
                        }
                        catch (IOException)
                        {
                            return 0L;
                        }
                    });
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>Deletes every cached payload, including payloads of older upstream versions.</summary>
        public static void Clear()
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PlayniteDlssgPlugin",
                "components");
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        /// <summary>
        /// Makes sure every requested optional file is present in the cache.
        /// Blocking; call it from a background thread.
        /// </summary>
        /// <param name="relativePaths">Files that must end up cached.</param>
        /// <param name="report">Receives (percent, status text).</param>
        /// <param name="token">Cancellation token from the progress dialog.</param>
        public static void Ensure(IReadOnlyList<string> relativePaths, Action<int, string> report, CancellationToken token)
        {
            EnableModernTls();
            RemoveStaleVersions();

            var pending = relativePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(path => !IsCached(path))
                .ToList();

            if (pending.Count == 0)
            {
                return;
            }

            var rawPending = pending.Where(path => !IsZipOnly(path)).ToList();
            var zipPending = pending.Where(IsZipOnly).ToList();
            var steps = rawPending.Count + (zipPending.Count > 0 ? 1 : 0);
            var completed = 0;

            foreach (var relativePath in rawPending)
            {
                token.ThrowIfCancellationRequested();
                report(Percent(completed, steps), Path.GetFileName(relativePath));
                DownloadFile(
                    RawBaseUrl + relativePath.Replace('\\', '/'),
                    GetCachedPath(relativePath),
                    null);
                completed++;
            }

            if (zipPending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                report(Percent(completed, steps), "dlssg-release-" + UpstreamVersion + "-x64.zip");

                var zipPath = Path.Combine(CacheRoot, ".release.zip");
                try
                {
                    DownloadFile(ReleaseZipUrl, zipPath, null);
                    ExtractFromArchive(zipPath, zipPending);
                }
                finally
                {
                    TryDelete(zipPath);
                    TryDelete(zipPath + ".part");
                }
            }

            report(100, string.Empty);
        }

        private static int Percent(int completed, int steps)
        {
            if (steps <= 0)
            {
                return 100;
            }

            return (completed * 100) / steps;
        }

        private static void ExtractFromArchive(string zipPath, IReadOnlyList<string> relativePaths)
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var relativePath in relativePaths)
                {
                    var wanted = relativePath.Replace('\\', '/');

                    // The archive is packed with a top-level folder whose name follows
                    // the release tag, so match on the tail of the entry path.
                    var entry = archive.Entries.FirstOrDefault(candidate =>
                    {
                        var name = candidate.FullName.Replace('\\', '/');
                        return string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase) ||
                               name.EndsWith("/" + wanted, StringComparison.OrdinalIgnoreCase);
                    });

                    if (entry == null)
                    {
                        throw new InvalidOperationException(
                            Path.GetFileName(relativePath) + " is missing from " +
                            Path.GetFileName(zipPath) + " (upstream layout changed?).");
                    }

                    var target = GetCachedPath(relativePath);
                    var targetDirectory = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    entry.ExtractToFile(target, true);
                }
            }
        }

        private static void DownloadFile(string url, string targetPath, Action<long, long> progress)
        {
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var temporaryPath = targetPath + ".part";

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PlayniteDlssgPlugin/" + UpstreamVersion);

                using (var response = client
                    .GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
                    .GetAwaiter()
                    .GetResult())
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException(
                            "HTTP " + (int)response.StatusCode + " for " + url);
                    }

                    var total = response.Content.Headers.ContentLength ?? -1L;

                    using (var input = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                    using (var output = File.Create(temporaryPath))
                    {
                        var buffer = new byte[81920];
                        long received = 0;
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, read);
                            received += read;
                            if (progress != null)
                            {
                                progress(received, total);
                            }
                        }
                    }
                }
            }

            TryDelete(targetPath);
            File.Move(temporaryPath, targetPath);
        }

        /// <summary>Drops payloads cached for a different upstream version.</summary>
        private static void RemoveStaleVersions()
        {
            try
            {
                var componentsRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PlayniteDlssgPlugin",
                    "components");

                if (!Directory.Exists(componentsRoot))
                {
                    return;
                }

                foreach (var directory in Directory.GetDirectories(componentsRoot))
                {
                    if (!string.Equals(
                        Path.GetFileName(directory),
                        UpstreamVersion,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.Delete(directory, true);
                    }
                }
            }
            catch (Exception)
            {
                // Cache hygiene is best effort - a locked file must never block a deploy.
            }
        }

        private static void EnableModernTls()
        {
            try
            {
                // Windows PowerShell era defaults: GitHub refuses anything below TLS 1.2.
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch (NotSupportedException)
            {
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
