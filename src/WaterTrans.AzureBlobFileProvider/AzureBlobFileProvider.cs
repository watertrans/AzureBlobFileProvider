using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace WaterTrans.AzureBlobFileProvider
{
    /// <summary>
    /// Looks up files using the azure blob container
    /// </summary>
    public class AzureBlobFileProvider : IFileProvider
    {
        private const string TEMP_SUBPATH = "AzureBlobFileProvider";
        private readonly BlobServiceClient _serviceClient;
        private readonly BlobContainerClient _containerClient;
        private readonly PhysicalFileProvider _physicalFileProvider;
        private readonly string _localCacheRoot;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _localCacheTimeouts;
        private readonly int _localCacheTimeoutInSeconds;

        /// <summary>
        /// Initializes an instance of <see cref="AzureBlobFileProvider"/>
        /// </summary>
        /// <param name="options">The <see cref="AzureBlobOptions"/>.</param>
        public AzureBlobFileProvider(AzureBlobOptions options)
        {
            _localCacheTimeouts = new ConcurrentDictionary<string, DateTimeOffset>();
            _localCacheTimeoutInSeconds = options.LocalCacheTimeout;

            if (options.ConnectionString != null)
            {
                _serviceClient = new BlobServiceClient(options.ConnectionString, options.BlobClientOptions);
            }
            else if (options.ServiceUri != null && options.Token != null)
            {
                _serviceClient = new BlobServiceClient(options.ServiceUri, new AzureSasCredential(options.Token), options.BlobClientOptions);
            }
            else
            {
                throw new ArgumentException("Must be set 'ConnectionString' or 'ServiceUri' + 'Signature'.");
            }

            _containerClient = _serviceClient.GetBlobContainerClient(options.ContainerName);

            if (string.IsNullOrEmpty(options.LocalCacheRoot))
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
                _localCacheRoot = options.LocalCacheRoot;
                _physicalFileProvider = new PhysicalFileProvider(options.LocalCacheRoot);
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
            string relativePath = subpath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = GetFullPath(_localCacheRoot, relativePath);
            IFileInfo localCacheFileInfo = _physicalFileProvider.GetFileInfo(subpath);

            // Check the timeout of the local cache.
            _localCacheTimeouts.TryGetValue(fullPath.ToLower(), out timeout);
            if (localCacheFileInfo.Exists && DateTimeOffset.UtcNow < timeout)
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

                var newTimeout = DateTimeOffset.UtcNow.AddSeconds(_localCacheTimeoutInSeconds);
                _localCacheTimeouts.AddOrUpdate(fullPath.ToLower(), newTimeout, (key, value) =>
                {
                    return newTimeout;
                });

                return blobFileInfo;
            }
            else
            {
                return new NotFoundFileInfo(subpath);
            }
        }

        /// <inheritdoc />
        public IChangeToken Watch(string filter) => null;

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

