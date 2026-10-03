// -------------------------------------------------------------------------------------------------
//   <copyright file="IArgusMessageSource.cs">
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
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A source of ARGUS/1.0 message parts, so request and response deserialization can share one code path
    /// for byte-level (<see cref="ArgusWireReader"/>) and text-level (<see cref="ArgusTextMessageSource"/>) input
    /// </summary>
    internal interface IArgusMessageSource
    {
        /// <summary>
        /// Reads the next line of the header block
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The line without its terminator, or <c>null</c> at the end of the input
        /// </returns>
        ValueTask<string> ReadLineAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Reads a <c>Content-Length</c> delimited body as text
        /// </summary>
        /// <param name="byteCount">
        /// The body length in bytes
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The body text
        /// </returns>
        Task<string> ReadBodyAsync(int byteCount, CancellationToken cancellationToken);

        /// <summary>
        /// Reads a body in chunked transfer encoding
        /// </summary>
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes. A value of 0 disables the limit.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="MemoryStream"/> containing the de-chunked data, positioned at the beginning
        /// </returns>
        Task<MemoryStream> ReadChunkedAsync(long maxBodySize, CancellationToken cancellationToken);
    }
}
