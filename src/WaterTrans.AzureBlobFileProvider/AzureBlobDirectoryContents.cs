using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.FileProviders;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WaterTrans.AzureBlobFileProvider
{
    /// <summary>
    /// Represents the contents of an azure blob directory
    /// </summary>
    public class AzureBlobDirectoryContents : IDirectoryContents
    {
        private readonly BlobContainerClient _containerClient;
        private readonly List<BlobItem> _blobs = new List<BlobItem>();

        /// <summary>
        /// Initializes an instance of <see cref="AzureBlobDirectoryContents"/>
        /// </summary>
        /// <param name="containerClient">The <see cref="BlobContainerClient"/>.</param>
        /// <param name="subpath">Relative path that identifies the directory.</param>
        public AzureBlobDirectoryContents(BlobContainerClient containerClient, string subpath)
        {
            subpath = subpath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            _containerClient = containerClient;
            foreach (BlobItem blobItem in containerClient.GetBlobs(prefix: subpath))
            {
                _blobs.Add(blobItem);
            }
            Exists = _blobs.Count > 0;
        }

        /// <inheritdoc />
        public IEnumerator<IFileInfo> GetEnumerator()
        {
            return _blobs.Select(blob => new AzureBlobFileInfo(_containerClient.GetBlobClient(blob.Name), blob)).GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        public bool Exists { get; set; }
    }
}

