// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusChunkedWriteStream.cs">
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
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A write-only <see cref="Stream"/> that writes every non-empty write to the destination as one chunk in chunked
    /// transfer encoding. It does not write the terminating zero-length chunk and does not close the destination.
    /// </summary>
    internal sealed class ArgusChunkedWriteStream : Stream
    {
        /// <summary>
        /// The bytes of the line terminator
        /// </summary>
        private static readonly byte[] CrLf = { (byte)'\r', (byte)'\n' };

        /// <summary>
        /// The <see cref="Stream"/> that receives the chunks
        /// </summary>
        private readonly Stream destination;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusChunkedWriteStream"/> class
        /// </summary>
        /// <param name="destination">
        /// The <see cref="Stream"/> that receives the chunks
        /// </param>
        public ArgusChunkedWriteStream(Stream destination)
        {
            this.destination = destination;
        }

        /// <summary>
        /// Gets a value indicating whether the stream supports reading: always <c>false</c>
        /// </summary>
        public override bool CanRead => false;

        /// <summary>
        /// Gets a value indicating whether the stream supports seeking: always <c>false</c>
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// Gets a value indicating whether the stream supports writing: always <c>true</c>
        /// </summary>
        public override bool CanWrite => true;

        /// <summary>
        /// Not supported
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Always thrown
        /// </exception>
        public override long Length => throw new NotSupportedException();

        /// <summary>
        /// Not supported
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Always thrown
        /// </exception>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Writes the bytes as one chunk; an empty write writes nothing, because a zero-length chunk ends the body
        /// </summary>
        /// <param name="buffer">
        /// The buffer containing the data
        /// </param>
        /// <param name="offset">
        /// The offset of the first byte to write
        /// </param>
        /// <param name="count">
        /// The number of bytes to write
        /// </param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.Write(buffer.AsSpan(offset, count));
        }

        /// <summary>
        /// Writes the bytes as one chunk; an empty write writes nothing, because a zero-length chunk ends the body
        /// </summary>
        /// <param name="buffer">
        /// The data to write
        /// </param>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                return;
            }

            this.destination.Write(SizeLine(buffer.Length));
            this.destination.Write(buffer);
            this.destination.Write(CrLf);
        }

        /// <summary>
        /// Asynchronously writes the bytes as one chunk; an empty write writes nothing, because a zero-length chunk ends the body
        /// </summary>
        /// <param name="buffer">
        /// The buffer containing the data
        /// </param>
        /// <param name="offset">
        /// The offset of the first byte to write
        /// </param>
        /// <param name="count">
        /// The number of bytes to write
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        /// <summary>
        /// Asynchronously writes the bytes as one chunk; an empty write writes nothing, because a zero-length chunk ends the body
        /// </summary>
        /// <param name="buffer">
        /// The data to write
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation
        /// </returns>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty)
            {
                return;
            }

            await this.destination.WriteAsync(SizeLine(buffer.Length).AsMemory(), cancellationToken);
            await this.destination.WriteAsync(buffer, cancellationToken);
            await this.destination.WriteAsync(CrLf.AsMemory(), cancellationToken);
        }

        /// <summary>
        /// Flushes the destination stream
        /// </summary>
        public override void Flush()
        {
            this.destination.Flush();
        }

        /// <summary>
        /// Asynchronously flushes the destination stream
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return this.destination.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Not supported
        /// </summary>
        /// <param name="buffer">
        /// Unused
        /// </param>
        /// <param name="offset">
        /// Unused
        /// </param>
        /// <param name="count">
        /// Unused
        /// </param>
        /// <returns>
        /// Never returns
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// Always thrown
        /// </exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Not supported
        /// </summary>
        /// <param name="offset">
        /// Unused
        /// </param>
        /// <param name="origin">
        /// Unused
        /// </param>
        /// <returns>
        /// Never returns
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// Always thrown
        /// </exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Not supported
        /// </summary>
        /// <param name="value">
        /// Unused
        /// </param>
        /// <exception cref="NotSupportedException">
        /// Always thrown
        /// </exception>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Builds the chunk-size line for a chunk
        /// </summary>
        /// <param name="length">
        /// The chunk length in bytes
        /// </param>
        /// <returns>
        /// The hexadecimal size followed by a line terminator, as ASCII bytes
        /// </returns>
        private static byte[] SizeLine(int length)
        {
            return Encoding.ASCII.GetBytes(length.ToString("x", CultureInfo.InvariantCulture) + "\r\n");
        }
    }
}
