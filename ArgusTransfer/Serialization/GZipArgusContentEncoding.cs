// ------------------------------------------------------------------------------------------------
// <copyright file="GZipArgusContentEncoding.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Serialization
{
    using System.IO;
    using System.IO.Compression;

    /// <summary>
    /// The gzip <see cref="IArgusContentEncoding"/>, implemented with <see cref="GZipStream"/>
    /// </summary>
    public class GZipArgusContentEncoding : IArgusContentEncoding
    {
        /// <summary>
        /// The gzip encoding token: "gzip"
        /// </summary>
        public const string EncodingName = "gzip";

        /// <summary>
        /// Gets the encoding token: "gzip"
        /// </summary>
        public string Name => EncodingName;

        /// <summary>
        /// Gets or sets the <see cref="System.IO.Compression.CompressionLevel"/> used when compressing.
        /// Defaults to <see cref="CompressionLevel.Fastest"/>, which suits local inter-process communication
        /// where CPU time matters more than the last few percent of size.
        /// </summary>
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Fastest;

        /// <summary>
        /// Creates a write-only <see cref="GZipStream"/> that compresses into <paramref name="destination"/>
        /// and leaves it open when disposed
        /// </summary>
        /// <param name="destination">
        /// The <see cref="Stream"/> that receives the compressed data
        /// </param>
        /// <returns>
        /// The compressing <see cref="Stream"/>
        /// </returns>
        public Stream CreateCompressionStream(Stream destination)
        {
            return new GZipStream(destination, this.CompressionLevel, leaveOpen: true);
        }

        /// <summary>
        /// Creates a read-only <see cref="GZipStream"/> that decompresses from <paramref name="source"/>
        /// and leaves it open when disposed
        /// </summary>
        /// <param name="source">
        /// The <see cref="Stream"/> that provides the compressed data
        /// </param>
        /// <returns>
        /// The decompressing <see cref="Stream"/>
        /// </returns>
        public Stream CreateDecompressionStream(Stream source)
        {
            return new GZipStream(source, CompressionMode.Decompress, leaveOpen: true);
        }
    }
}
