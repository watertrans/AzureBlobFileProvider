using Azure.Storage.Blobs;
using System;

namespace WaterTrans.AzureBlobFileProvider
{
    /// <summary>
    /// Options used to create an azure blob file provider
    /// </summary>
    public class AzureBlobOptions
    {
        /// <summary>
        /// The URI of the azure blob service.
        /// </summary>
        public Uri ServiceUri { get; set; }

        /// <summary>
        /// The shared access signature token used by the azure blob service.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// The name of the container to serve.
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// The connection string to the azure blob storage.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The <see cref="BlobClientOptions"/>.
        /// </summary>
        public BlobClientOptions BlobClientOptions { get; set; }

        /// <summary>
        /// The root directory of the local cache. If null is specified, a temporary directory will be used instead. This should be an absolute path.
        /// </summary>
        public string LocalCacheRoot { get; set; }

        /// <summary>
        /// Specifies the timeout of the local cache in seconds. The default is 300 seconds.
        /// </summary>
        public int LocalCacheTimeout { get; set; } = 300;

        /// <summary>
        /// Specifies the key of query parameter to be ignored by local caching.
        /// </summary>
        public string IgnoreCacheQueryKey { get; set; } = "ignoreCache";
    }
}