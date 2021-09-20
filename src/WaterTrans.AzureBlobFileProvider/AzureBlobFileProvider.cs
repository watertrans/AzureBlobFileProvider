using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;

namespace WaterTrans.AzureBlobFileProvider
{
    /// <summary>
    /// Looks up files using the azure blob container
    /// </summary>
    public class AzureBlobFileProvider : IFileProvider
    {
        private const string TEMP_SUBPATH = "AzureBlobFileProvider";
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly BlobServiceClient _serviceClient;
        private readonly BlobContainerClient _containerClient;
        private readonly PhysicalFileProvider _physicalFileProvider;
        private readonly string _localCacheRoot;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _localCacheTimeouts;
        private readonly int _localCacheTimeoutInSeconds;
        private readonly char[] _escapeChars;
        private readonly string _ignoreCacheQueryKey;

        /// <summary>
        /// Initializes an instance of <see cref="AzureBlobFileProvider"/>
        /// </summary>
        /// <param name="httpContextAccessor">The <see cref="IHttpContextAccessor"/>.</param>
        /// <param name="options">The configuration options.</param>
        public AzureBlobFileProvider(IHttpContextAccessor httpContextAccessor, IOptions<AzureBlobOptions> options)
        {
            var blobOptions = options.Value;

            if (string.IsNullOrEmpty(blobOptions.ContainerName))
            {
                throw new ArgumentException($"{nameof(AzureBlobOptions.ContainerName)} cannot be empty or null.");
            }

            if (string.IsNullOrEmpty(blobOptions.IgnoreCacheQueryKey))
            {
                throw new ArgumentException($"{nameof(AzureBlobOptions.IgnoreCacheQueryKey)} cannot be empty or null.");
            }

            var invalidChars = Path.GetInvalidPathChars().Union(Path.GetInvalidFileNameChars());
            _escapeChars = invalidChars.Where(c => c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar).ToArray();
            _localCacheTimeouts = new ConcurrentDictionary<string, DateTimeOffset>();
            _localCacheTimeoutInSeconds = blobOptions.LocalCacheTimeout;
            _ignoreCacheQueryKey = blobOptions.IgnoreCacheQueryKey;
            _httpContextAccessor = httpContextAccessor;

            if (blobOptions.ConnectionString != null)
            {
                _serviceClient = new BlobServiceClient(blobOptions.ConnectionString, blobOptions.BlobClientOptions);
            }
            else if (blobOptions.ServiceUri != null && blobOptions.Token != null)
            {
                _serviceClient = new BlobServiceClient(blobOptions.ServiceUri, new AzureSasCredential(blobOptions.Token), blobOptions.BlobClientOptions);
            }
            else
            {
                throw new ArgumentException("Must be set 'ConnectionString' or 'ServiceUri' + 'Token'.");
            }

            _containerClient = _serviceClient.GetBlobContainerClient(blobOptions.ContainerName);

            if (string.IsNullOrEmpty(blobOptions.LocalCacheRoot))
            {
                _localCacheRoot = Path.Combine(Path.GetTempPath(), TEMP_SUBPATH, _containerClient.AccountName, _containerClient.Name);

                if (!Directory.Exists(_localCacheRoot))
                {
                    Directory.CreateDirectory(_localCacheRoot);
                }

                _physicalFileProvider = new PhysicalFileProvider(_localCacheRoot);
            }
            else
            {
                _localCacheRoot = blobOptions.LocalCacheRoot;
                _physicalFileProvider = new PhysicalFileProvider(blobOptions.LocalCacheRoot);
            }
        }

        /// <inheritdoc />
        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            return new AzureBlobDirectoryContents(_containerClient, subpath);
        }

        /// <inheritdoc />
        public IFileInfo GetFileInfo(string subpath)
        {
            DateTimeOffset timeout = DateTimeOffset.MinValue;
            string escapedSubpath = EscapeInvalidPathChars(subpath);
            string escapedRelativePath = escapedSubpath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string relativePath = subpath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = GetFullPath(_localCacheRoot, escapedRelativePath);
            string cacheKey = fullPath.ToLower();
            IFileInfo localCacheFileInfo = _physicalFileProvider.GetFileInfo(escapedSubpath);

            // Check the value of the query parameter to ignore local caching.
            if (!Boolean.TryParse(_httpContextAccessor.HttpContext.Request.Query[_ignoreCacheQueryKey], out bool ignoreCache))
            {
                ignoreCache = false;
            }

            // Check the timeout of the local cache.
            _localCacheTimeouts.TryGetValue(cacheKey, out timeout);
            if (!ignoreCache && localCacheFileInfo.Exists && DateTimeOffset.UtcNow < timeout)
            {
                return localCacheFileInfo;
            }

            BlobItem blob = _containerClient.GetBlobs(prefix: relativePath).FirstOrDefault();
            if (blob != null)
            {
                var blobFileInfo = new AzureBlobFileInfo(_containerClient.GetBlobClient(relativePath), blob);

                // If the local cache does not exist or has been modified, download it.
                if (fullPath != null &&
                   (!localCacheFileInfo.Exists ||
                     localCacheFileInfo.Length != blobFileInfo.Length ||
                     localCacheFileInfo.LastModified != blobFileInfo.LastModified))
                {
                    CopyToLocalCache(fullPath, blobFileInfo.CreateReadStream(), blobFileInfo.LastModified);
                }

                if (!ignoreCache)
                {
                    var newTimeout = DateTimeOffset.UtcNow.AddSeconds(_localCacheTimeoutInSeconds);
                    _localCacheTimeouts.AddOrUpdate(cacheKey, newTimeout, (key, value) =>
                    {
                        return newTimeout;
                    });
                }

                return blobFileInfo;
            }
            else
            {
                return new NotFoundFileInfo(subpath);
            }
        }

        /// <inheritdoc />
        public IChangeToken Watch(string filter) => null;

        private string EscapeInvalidPathChars(string path)
        {
            var result = new StringBuilder();
            foreach(char c in path)
            {
                if (_escapeChars.Contains(c))
                {
                    result.Append(Uri.HexEscape(c));
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }

        private static bool PathNavigatesAboveRoot(string path)
        {
            var tokenizer = new StringTokenizer(path, new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            int depth = 0;

            foreach (StringSegment segment in tokenizer)
            {
                if (segment.Equals(".") || segment.Equals(""))
                {
                    continue;
                }
                else if (segment.Equals(".."))
                {
                    depth--;

                    if (depth == -1)
                    {
                        return true;
                    }
                }
                else
                {
                    depth++;
                }
            }

            return false;
        }

        private static void CopyToLocalCache(string fullPath, Stream stream, DateTimeOffset lastModified)
        {
            try
            {
                var tempPath = Path.GetTempFileName();
                using (var fileStream = File.Create(tempPath))
                {
                    stream.CopyTo(fileStream);
                }

                File.SetLastWriteTimeUtc(tempPath, lastModified.UtcDateTime);

                var directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.Copy(tempPath, fullPath, true);
                File.Delete(tempPath);
            }
            catch (Exception)
            {
                // If the file fails to write, the exception raised is ignored.
            }
            finally
            {
                stream.Close();
            }
        }

        private static string GetFullPath(string root, string path)
        {
            if (PathNavigatesAboveRoot(path))
            {
                return null;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(Path.Combine(root, path));
            }
            catch
            {
                return null;
            }

            if (!IsUnderneathRoot(root, fullPath))
            {
                return null;
            }

            return fullPath;
        }

        private static bool IsUnderneathRoot(string root, string fullPath)
        {
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
    }
}

