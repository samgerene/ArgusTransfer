// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusTextMessageSource.cs">
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
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IArgusMessageSource"/> over a <see cref="StreamReader"/>. <c>Content-Length</c> is counted in
    /// UTF-8 bytes; chunked bodies use the text-based <see cref="ArgusChunkedEncoding"/> methods and therefore only
    /// round-trip valid UTF-8 text.
    /// </summary>
    internal sealed class ArgusTextMessageSource : IArgusMessageSource
    {
        /// <summary>
        /// The <see cref="StreamReader"/> to read from
        /// </summary>
        private readonly StreamReader reader;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusTextMessageSource"/> class
        /// </summary>
        /// <param name="reader">
        /// The <see cref="StreamReader"/> to read from
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="reader"/> is <c>null</c>
        /// </exception>
        public ArgusTextMessageSource(StreamReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            this.reader = reader;
        }

        /// <summary>
        /// Reads the next line of the header block. The length is checked after <see cref="StreamReader"/> has read the
        /// whole line, so unlike <see cref="ArgusWireReader"/> this does not bound the memory used while reading it.
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
        public async ValueTask<string> ReadLineAsync(int maxLength, CancellationToken cancellationToken)
        {
            var line = await this.reader.ReadLineAsync(cancellationToken);

            if (line != null && Encoding.UTF8.GetByteCount(line) > maxLength)
            {
                throw new ArgusLineTooLongException(maxLength);
            }

            return line;
        }

        /// <summary>
        /// Reads a <c>Content-Length</c> delimited body as text, counting <paramref name="byteCount"/> in UTF-8 bytes
        /// </summary>
        /// <param name="byteCount">
        /// The body length in bytes
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The body text; shorter than <paramref name="byteCount"/> bytes when the reader ends early
        /// </returns>
        public Task<string> ReadBodyAsync(int byteCount, CancellationToken cancellationToken)
        {
            return ArgusTextBodyReader.ReadAsync(this.reader, byteCount, cancellationToken);
        }

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
        public Task<MemoryStream> ReadChunkedAsync(long maxBodySize, CancellationToken cancellationToken)
        {
            return ArgusChunkedEncoding.ReadChunkedAsync(this.reader, maxBodySize, cancellationToken);
        }
    }
}
