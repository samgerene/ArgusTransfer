// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusWireReader.cs">
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
    /// Reads an ARGUS/1.0 message from a <see cref="Stream"/> at the byte level: header lines are decoded as UTF-8,
    /// while body bytes are returned unchanged, so <c>Content-Length</c> and chunk sizes are honored as byte counts.
    /// Reads from the underlying stream only when more bytes are needed for the current message, so it never blocks
    /// waiting for data beyond the end of the message.
    /// </summary>
    internal sealed class ArgusWireReader : IArgusMessageSource
    {
        /// <summary>
        /// The size of the internal read buffer in bytes
        /// </summary>
        private const int BufferSize = 8192;

        /// <summary>
        /// The <see cref="Stream"/> to read from
        /// </summary>
        private readonly Stream stream;

        /// <summary>
        /// The internal read buffer
        /// </summary>
        private readonly byte[] buffer = new byte[BufferSize];

        /// <summary>
        /// The position of the next unread byte in <see cref="buffer"/>
        /// </summary>
        private int position;

        /// <summary>
        /// The number of valid bytes in <see cref="buffer"/>
        /// </summary>
        private int length;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusWireReader"/> class
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> to read from
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> is <c>null</c>
        /// </exception>
        public ArgusWireReader(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            this.stream = stream;
        }

        /// <summary>
        /// Reads a line terminated by <c>\n</c> (optionally preceded by <c>\r</c>) and decodes it as UTF-8. The line is
        /// rejected as soon as it grows beyond <paramref name="maxLength"/>, so a peer cannot make the reader buffer an
        /// unbounded line.
        /// </summary>
        /// <param name="maxLength">
        /// The maximum length of the line in bytes, excluding its terminator
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The line without its terminator, or <c>null</c> when the stream ended before any byte of the line was read.
        /// A final line without a terminator is returned as is.
        /// </returns>
        /// <exception cref="ArgusLineTooLongException">
        /// Thrown when the line is longer than <paramref name="maxLength"/> bytes
        /// </exception>
        public async ValueTask<string> ReadLineAsync(int maxLength, CancellationToken cancellationToken)
        {
            using var line = new MemoryStream();

            while (true)
            {
                if (this.position == this.length && !await this.FillAsync(cancellationToken))
                {
                    return line.Length == 0 ? null : Decode(line, maxLength);
                }

                var newLineIndex = Array.IndexOf(this.buffer, (byte)'\n', this.position, this.length - this.position);

                if (newLineIndex < 0)
                {
                    await line.WriteAsync(this.buffer.AsMemory(this.position, this.length - this.position), cancellationToken);
                    this.position = this.length;

                    // Allow one extra byte for a '\r' that may precede the terminator
                    if (line.Length > (long)maxLength + 1)
                    {
                        throw new ArgusLineTooLongException(maxLength);
                    }

                    continue;
                }

                await line.WriteAsync(this.buffer.AsMemory(this.position, newLineIndex - this.position), cancellationToken);
                this.position = newLineIndex + 1;

                return Decode(line, maxLength);
            }
        }

        /// <summary>
        /// Reads exactly <paramref name="destination"/>.Length bytes
        /// </summary>
        /// <param name="destination">
        /// The memory to fill
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        /// <exception cref="EndOfStreamException">
        /// Thrown when the stream ends before <paramref name="destination"/> is filled
        /// </exception>
        public async Task ReadExactlyAsync(Memory<byte> destination, CancellationToken cancellationToken)
        {
            var filled = 0;

            while (filled < destination.Length)
            {
                if (this.position == this.length && !await this.FillAsync(cancellationToken))
                {
                    throw new EndOfStreamException(
                        $"The stream ended after {filled} of {destination.Length} expected body bytes.");
                }

                var count = Math.Min(this.length - this.position, destination.Length - filled);
                this.buffer.AsMemory(this.position, count).CopyTo(destination.Slice(filled));

                this.position += count;
                filled += count;
            }
        }

        /// <summary>
        /// Reads exactly <paramref name="byteCount"/> body bytes and decodes them as UTF-8
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
        /// <exception cref="EndOfStreamException">
        /// Thrown when the stream ends before <paramref name="byteCount"/> bytes were read
        /// </exception>
        public async Task<string> ReadBodyAsync(int byteCount, CancellationToken cancellationToken)
        {
            return Encoding.UTF8.GetString(await this.ReadBytesAsync(byteCount, cancellationToken));
        }

        /// <summary>
        /// Reads exactly <paramref name="byteCount"/> bytes
        /// </summary>
        /// <param name="byteCount">
        /// The number of bytes to read
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The bytes read
        /// </returns>
        /// <exception cref="EndOfStreamException">
        /// Thrown when the stream ends before <paramref name="byteCount"/> bytes were read
        /// </exception>
        public async Task<byte[]> ReadBytesAsync(int byteCount, CancellationToken cancellationToken)
        {
            var bytes = new byte[byteCount];
            await this.ReadExactlyAsync(bytes.AsMemory(), cancellationToken);

            return bytes;
        }

        /// <summary>
        /// Reads a body in chunked transfer encoding as raw bytes
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
            return ArgusChunkedEncoding.ReadChunkedAsync(this, maxBodySize, cancellationToken);
        }

        /// <summary>
        /// Decodes the bytes of a line as UTF-8, removing a trailing <c>\r</c>
        /// </summary>
        /// <param name="line">
        /// The <see cref="MemoryStream"/> containing the bytes of the line
        /// </param>
        /// <param name="maxLength">
        /// The maximum length of the line in bytes, excluding its terminator
        /// </param>
        /// <returns>
        /// The decoded line
        /// </returns>
        /// <exception cref="ArgusLineTooLongException">
        /// Thrown when the line is longer than <paramref name="maxLength"/> bytes
        /// </exception>
        private static string Decode(MemoryStream line, int maxLength)
        {
            var bytes = line.GetBuffer();
            var count = (int)line.Length;

            if (count > 0 && bytes[count - 1] == (byte)'\r')
            {
                count--;
            }

            if (count > maxLength)
            {
                throw new ArgusLineTooLongException(maxLength);
            }

            return Encoding.UTF8.GetString(bytes, 0, count);
        }

        /// <summary>
        /// Refills the internal buffer from the underlying stream
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// <c>true</c> if at least one byte was read; <c>false</c> at the end of the stream
        /// </returns>
        private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
        {
            this.length = await this.stream.ReadAsync(this.buffer.AsMemory(), cancellationToken);
            this.position = 0;

            return this.length > 0;
        }
    }
}
