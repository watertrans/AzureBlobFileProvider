using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.FileProviders;
using System;
using System.IO;

namespace WaterTrans.AzureBlobFileProvider
{
    /// <summary>
    /// Represents a file on an azure blob item
    /// </summary>
    public class AzureBlobFileInfo : IFileInfo
    {
        private readonly BlobClient _blobClient;

        /// <summary>
        /// Initializes an instance of <see cref="AzureBlobFileInfo"/>
        /// </summary>
        /// <param name="blobClient">The <see cref="BlobClient"/>.</param>
        /// <param name="blob">The <see cref="BlobItem"/>.</param>
        public AzureBlobFileInfo(BlobClient blobClient, BlobItem blob)
        {
            _blobClient = blobClient;
            Name = blob.Name;
            Exists = true;
            Length = blob.Properties.ContentLength ?? -1;
            LastModified = blob.Properties.LastModified ?? DateTimeOffset.MinValue;
        }

        /// <inheritdoc />
        public Stream CreateReadStream()
        {
            return _blobClient.OpenRead(new BlobOpenReadOptions(true));
        }

        /// <inheritdoc />
        public bool Exists { get; }

        /// <inheritdoc />
        public long Length { get; }

        /// <inheritdoc />
        public string PhysicalPath { get; }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public DateTimeOffset LastModified { get; }

        /// <summary>
        /// Always false.
        /// </summary>
        public bool IsDirectory => false;
    }
}

