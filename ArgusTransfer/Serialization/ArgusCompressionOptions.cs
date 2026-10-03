// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusCompressionOptions.cs">
//
//     Copyright (c) 2025-2026 Sam Gerené
//
//     Licensed under the Apache License, Version 2.0 (the "License");
//     you may not use this file except in compliance with the License.
//     You may obtain a copy of the License at
//
//         http://www.apache.org/licenses/LICENSE-2.0
//
//     Unless required by applicable law or agreed to in writing, softwareUseCases
//     distributed under the License is distributed on an "AS IS" BASIS,
//     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//     See the License for the specific language governing permissions and
//     limitations under the License.
//
//   </copyright>
//   ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Serialization
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Configures body compression with <c>Content-Encoding</c> for <c>ArgusClient</c> and the pipe host
    /// </summary>
    /// <remarks>
    /// <para>
    /// Decoding does not depend on <see cref="Enabled"/>: a received body whose <c>Content-Encoding</c> matches one of
    /// the <see cref="Encodings"/> is always decompressed.
    /// </para>
    /// <para>
    /// On the client, <see cref="Enabled"/> sends an <c>Accept-Encoding</c> header listing the <see cref="Encodings"/> and
    /// compresses request bodies of at least <see cref="MinimumBodySize"/> bytes. Only enable it when the server supports
    /// the encoding. On the pipe host, <see cref="Enabled"/> compresses response bodies of at least
    /// <see cref="MinimumBodySize"/> bytes when the request's <c>Accept-Encoding</c> header accepts one of the
    /// <see cref="Encodings"/>.
    /// </para>
    /// </remarks>
    public class ArgusCompressionOptions
    {
        /// <summary>
        /// Backing field for <see cref="MinimumBodySize"/>
        /// </summary>
        private int minimumBodySize = 1024;

        /// <summary>
        /// Backing field for <see cref="PreferredEncoding"/>
        /// </summary>
        private string preferredEncoding = GZipArgusContentEncoding.EncodingName;

        /// <summary>
        /// Gets or sets a value indicating whether outgoing bodies are compressed. Defaults to <c>false</c>.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the minimum body size in bytes for compression; smaller bodies are sent uncompressed because
        /// compressing them costs more than it saves. Defaults to 1024. A streamed body whose length is unknown is
        /// always compressed when compression applies.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is negative
        /// </exception>
        public int MinimumBodySize
        {
            get => this.minimumBodySize;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                this.minimumBodySize = value;
            }
        }

        /// <summary>
        /// Gets or sets the token of the encoding used to compress outgoing bodies when the other side accepts it.
        /// Must match the <see cref="IArgusContentEncoding.Name"/> of one of the <see cref="Encodings"/>. Defaults to "gzip".
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when the value is <c>null</c> or white space
        /// </exception>
        public string PreferredEncoding
        {
            get => this.preferredEncoding;
            set
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
                this.preferredEncoding = value;
            }
        }

        /// <summary>
        /// Gets the supported encodings, used to decode received bodies, to build the <c>Accept-Encoding</c> header and to
        /// compress outgoing bodies. Contains a <see cref="GZipArgusContentEncoding"/> by default; add custom
        /// <see cref="IArgusContentEncoding"/> implementations to support other encodings.
        /// </summary>
        public IList<IArgusContentEncoding> Encodings { get; } = new List<IArgusContentEncoding> { new GZipArgusContentEncoding() };
    }
}
