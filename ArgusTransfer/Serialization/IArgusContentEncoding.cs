// -------------------------------------------------------------------------------------------------
//   <copyright file="IArgusContentEncoding.cs">
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
    using System.IO;

    /// <summary>
    /// Defines a content encoding (for example gzip) that compresses message bodies on the wire,
    /// identified by its <c>Content-Encoding</c> / <c>Accept-Encoding</c> token
    /// </summary>
    public interface IArgusContentEncoding
    {
        /// <summary>
        /// Gets the encoding token used in the <c>Content-Encoding</c> and <c>Accept-Encoding</c> headers (e.g. "gzip").
        /// Tokens are compared case-insensitively.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Creates a write-only stream that compresses the data written to it into <paramref name="destination"/>.
        /// Disposing the returned stream must flush the remaining compressed data but leave <paramref name="destination"/> open.
        /// </summary>
        /// <param name="destination">
        /// The <see cref="Stream"/> that receives the compressed data
        /// </param>
        /// <returns>
        /// The compressing <see cref="Stream"/>
        /// </returns>
        Stream CreateCompressionStream(Stream destination);

        /// <summary>
        /// Creates a read-only stream that decompresses the data read from <paramref name="source"/>.
        /// Disposing the returned stream must leave <paramref name="source"/> open.
        /// </summary>
        /// <param name="source">
        /// The <see cref="Stream"/> that provides the compressed data
        /// </param>
        /// <returns>
        /// The decompressing <see cref="Stream"/>
        /// </returns>
        Stream CreateDecompressionStream(Stream source);
    }
}
