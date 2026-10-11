// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusChunkedWriteStreamTestFixture.cs">
//
//     Copyright (c) 2025-2026 Sam Gerené
//
//     Licensed under the Apache License, Version 2.0 (the "License");
//     you may not use this file except in compliance with the License.
//     You may obtain a copy of the License at
//
//         http://www.apache.org/licenses/LICENSE-2.0
//
//     Unless required by applicable law or agreed to in writing, software
//     distributed under the License is distributed on an "AS IS" BASIS,
//     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//     See the License for the specific language governing permissions and
//     limitations under the License.
//
//   </copyright>
//   ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusChunkedWriteStream"/> class
    /// </summary>
    [TestFixture]
    public class ArgusChunkedWriteStreamTestFixture
    {
        [Test]
        public async Task Verify_that_each_write_becomes_one_chunk()
        {
            using var destination = new MemoryStream();
            await using var chunked = new ArgusChunkedWriteStream(destination);

            chunked.Write(Encoding.ASCII.GetBytes("abc"), 0, 3);
            await chunked.WriteAsync(Encoding.ASCII.GetBytes("0123456789ABCDEF").AsMemory());

            Assert.That(Encoding.ASCII.GetString(destination.ToArray()), Is.EqualTo("3\r\nabc\r\n10\r\n0123456789ABCDEF\r\n"));
        }

        [Test]
        public async Task Verify_that_empty_writes_produce_no_chunk()
        {
            using var destination = new MemoryStream();
            await using var chunked = new ArgusChunkedWriteStream(destination);

            chunked.Write(Array.Empty<byte>(), 0, 0);
            await chunked.WriteAsync(ReadOnlyMemory<byte>.Empty);

            Assert.That(destination.Length, Is.Zero);
        }

        [Test]
        public void Verify_that_stream_is_write_only()
        {
            using var chunked = new ArgusChunkedWriteStream(new MemoryStream());

            Assert.That(chunked.CanWrite, Is.True);
            Assert.That(chunked.CanRead, Is.False);
            Assert.That(chunked.CanSeek, Is.False);
            Assert.That(() => chunked.Length, Throws.TypeOf<NotSupportedException>());
            Assert.That(() => chunked.Position, Throws.TypeOf<NotSupportedException>());
            Assert.That(() => chunked.Position = 0, Throws.TypeOf<NotSupportedException>());
            Assert.That(() => chunked.Read(new byte[1], 0, 1), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => chunked.Seek(0, SeekOrigin.Begin), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => chunked.SetLength(0), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => chunked.Flush(), Throws.Nothing);
        }

        [Test]
        public async Task Verify_that_write_without_a_token_uses_the_token_passed_to_the_constructor()
        {
            using var cts = new CancellationTokenSource();
            using var destination = new TokenRecordingStream();
            await using var chunked = new ArgusChunkedWriteStream(destination, cts.Token);

            await chunked.WriteAsync(Encoding.ASCII.GetBytes("abc").AsMemory());

            Assert.That(destination.Tokens, Has.Count.EqualTo(3));
            Assert.That(destination.Tokens, Has.All.EqualTo(cts.Token));
        }

        [Test]
        public async Task Verify_that_write_with_a_token_uses_that_token()
        {
            using var constructorCts = new CancellationTokenSource();
            using var writeCts = new CancellationTokenSource();
            using var destination = new TokenRecordingStream();
            await using var chunked = new ArgusChunkedWriteStream(destination, constructorCts.Token);

            await chunked.WriteAsync(Encoding.ASCII.GetBytes("abc").AsMemory(), writeCts.Token);

            Assert.That(destination.Tokens, Has.Count.EqualTo(3));
            Assert.That(destination.Tokens, Has.All.EqualTo(writeCts.Token));
        }

        [Test]
        public async Task Verify_that_cancelling_the_constructor_token_cancels_a_write_without_a_token()
        {
            using var cts = new CancellationTokenSource();
            using var destination = new MemoryStream();
            await using var chunked = new ArgusChunkedWriteStream(destination, cts.Token);

            await cts.CancelAsync();

            Assert.That(
                async () => await chunked.WriteAsync(Encoding.ASCII.GetBytes("abc").AsMemory()),
                Throws.InstanceOf<OperationCanceledException>());
            Assert.That(destination.Length, Is.Zero);
        }

        [Test]
        public async Task Verify_that_compressed_chunked_writing_passes_the_token_to_every_write()
        {
            using var cts = new CancellationTokenSource();
            using var destination = new TokenRecordingStream();

            // The compression stream writes its last block when it is disposed, without a token of its own; on an
            // unbuffered pipe that write must still be cancellable
            await ArgusChunkedEncoding.WriteChunkedAsync(new MemoryStream(new byte[200]), destination, new GZipArgusContentEncoding(), cts.Token);

            Assert.That(destination.Tokens, Is.Not.Empty);
            Assert.That(destination.Tokens, Has.All.EqualTo(cts.Token));
        }

        /// <summary>
        /// A <see cref="MemoryStream"/> that records the <see cref="CancellationToken"/> of every asynchronous write
        /// </summary>
        private sealed class TokenRecordingStream : MemoryStream
        {
            /// <summary>
            /// Gets the tokens of the asynchronous writes, in the order of the writes
            /// </summary>
            public List<CancellationToken> Tokens { get; } = new List<CancellationToken>();

            /// <summary>
            /// Records the token and writes the bytes
            /// </summary>
            /// <param name="buffer">
            /// The data to write
            /// </param>
            /// <param name="cancellationToken">
            /// The <see cref="CancellationToken"/> to record
            /// </param>
            /// <returns>
            /// A <see cref="ValueTask"/> representing the asynchronous operation
            /// </returns>
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                this.Tokens.Add(cancellationToken);

                return base.WriteAsync(buffer, cancellationToken);
            }
        }
    }
}
