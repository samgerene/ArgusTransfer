// ------------------------------------------------------------------------------------------------
// <copyright file="IArgusMessageSource.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

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
        /// <param name="maxLength">
        /// The maximum length of the line in UTF-8 bytes, excluding its terminator
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The line without its terminator, or <c>null</c> at the end of the input
        /// </returns>
        /// <exception cref="ArgusLineTooLongException">
        /// Thrown when the line is longer than <paramref name="maxLength"/> bytes
        /// </exception>
        ValueTask<string> ReadLineAsync(int maxLength, CancellationToken cancellationToken);

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
