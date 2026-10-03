// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusWireReaderTestFixture.cs">
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

namespace ArgusTransfer.Tests.Serialization
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusWireReader"/> class
    /// </summary>
    [TestFixture]
    public class ArgusWireReaderTestFixture
    {
        private const int NoLimit = int.MaxValue;

        /// <summary>
        /// A read-only stream that returns at most one byte per read, like a pipe delivering partial reads
        /// </summary>
        internal sealed class TrickleStream : MemoryStream
        {
            public TrickleStream(byte[] buffer) : base(buffer)
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return base.Read(buffer, offset, Math.Min(count, 1));
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return base.ReadAsync(buffer.Slice(0, Math.Min(buffer.Length, 1)), cancellationToken);
            }
        }

        [Test]
        public void Verify_that_constructor_throws_for_null_stream()
        {
            Assert.That(() => new ArgusWireReader(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public async Task Verify_that_ReadLineAsync_handles_CRLF_and_LF_terminators()
        {
            var reader = new ArgusWireReader(new MemoryStream(Encoding.UTF8.GetBytes("first\r\nsecond\nthird")));

            Assert.That(await reader.ReadLineAsync(NoLimit, CancellationToken.None), Is.EqualTo("first"));
            Assert.That(await reader.ReadLineAsync(NoLimit, CancellationToken.None), Is.EqualTo("second"));
            Assert.That(await reader.ReadLineAsync(NoLimit, CancellationToken.None), Is.EqualTo("third"));
            Assert.That(await reader.ReadLineAsync(NoLimit, CancellationToken.None), Is.Null);
        }

        [Test]
        public async Task Verify_that_ReadLineAsync_returns_empty_string_for_empty_line()
        {
            var reader = new ArgusWireReader(new MemoryStream(Encoding.UTF8.GetBytes("\r\n")));

            Assert.That(await reader.ReadLineAsync(NoLimit, CancellationToken.None), Is.Empty);
            Assert.That(await reader.ReadLineAsync(NoLimit, CancellationToken.None), Is.Null);
        }

        [Test]
        public async Task Verify_that_ReadLineAsync_returns_null_for_empty_stream()
        {
            var reader = new ArgusWireReader(new MemoryStream());

            Assert.That(await reader.ReadLineAsync(NoLimit, CancellationToken.None), Is.Null);
        }

        [Test]
        public async Task Verify_that_ReadLineAsync_decodes_multibyte_characters_split_across_reads()
        {
            var reader = new ArgusWireReader(new TrickleStream(Encoding.UTF8.GetBytes("X-Name: héllo €\r\n")));

            Assert.That(await reader.ReadLineAsync(NoLimit, CancellationToken.None), Is.EqualTo("X-Name: héllo €"));
        }

        [Test]
        public async Task Verify_that_ReadLineAsync_reads_lines_longer_than_the_buffer()
        {
            var longLine = new string('a', 20_000);
            var reader = new ArgusWireReader(new MemoryStream(Encoding.UTF8.GetBytes(longLine + "\r\nnext\r\n")));

            Assert.That(await reader.ReadLineAsync(NoLimit, CancellationToken.None), Is.EqualTo(longLine));
            Assert.That(await reader.ReadLineAsync(NoLimit, CancellationToken.None), Is.EqualTo("next"));
        }

        [Test]
        public async Task Verify_that_ReadExactlyAsync_returns_raw_bytes_after_a_line()
        {
            var payload = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
            var data = Encoding.ASCII.GetBytes("header\r\n").Concat(payload).ToArray();
            var reader = new ArgusWireReader(new MemoryStream(data));

            await reader.ReadLineAsync(NoLimit, CancellationToken.None);

            var body = new byte[256];
            await reader.ReadExactlyAsync(body.AsMemory(), CancellationToken.None);

            Assert.That(body, Is.EqualTo(payload));
        }

        [Test]
        public async Task Verify_that_ReadExactlyAsync_assembles_partial_reads()
        {
            var payload = Enumerable.Range(0, 300).Select(i => (byte)(i % 256)).ToArray();
            var reader = new ArgusWireReader(new TrickleStream(payload));

            var body = new byte[300];
            await reader.ReadExactlyAsync(body.AsMemory(), CancellationToken.None);

            Assert.That(body, Is.EqualTo(payload));
        }

        [Test]
        public async Task Verify_that_ReadExactlyAsync_spans_multiple_buffer_refills()
        {
            var payload = new byte[50_000];
            new Random(42).NextBytes(payload);
            var reader = new ArgusWireReader(new MemoryStream(payload));

            var body = new byte[payload.Length];
            await reader.ReadExactlyAsync(body.AsMemory(), CancellationToken.None);

            Assert.That(body, Is.EqualTo(payload));
        }

        [TestCase("12345\r\n")]
        [TestCase("12345\n")]
        [TestCase("12345")]
        public async Task Verify_that_ReadLineAsync_accepts_a_line_of_exactly_the_maximum_length(string data)
        {
            var reader = new ArgusWireReader(new MemoryStream(Encoding.ASCII.GetBytes(data)));

            Assert.That(await reader.ReadLineAsync(5, CancellationToken.None), Is.EqualTo("12345"));
        }

        [TestCase("123456\r\n")]
        [TestCase("123456")]
        public void Verify_that_ReadLineAsync_rejects_a_line_longer_than_the_maximum(string data)
        {
            var reader = new ArgusWireReader(new MemoryStream(Encoding.ASCII.GetBytes(data)));

            Assert.That(async () => await reader.ReadLineAsync(5, CancellationToken.None), Throws.InstanceOf<ArgusProtocolException>());
        }

        [Test]
        public void Verify_that_ReadLineAsync_stops_reading_an_endless_line_at_the_limit()
        {
            var stream = new EndlessStream();
            var reader = new ArgusWireReader(stream);

            Assert.That(async () => await reader.ReadLineAsync(16 * 1024, CancellationToken.None), Throws.InstanceOf<ArgusProtocolException>());
            Assert.That(stream.BytesRead, Is.LessThan(64 * 1024), "the reader must not keep buffering beyond the limit");
        }

        /// <summary>
        /// A stream that returns 'a' bytes forever and counts how many were read
        /// </summary>
        private sealed class EndlessStream : Stream
        {
            public long BytesRead { get; private set; }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                buffer.AsSpan(offset, count).Fill((byte)'a');
                this.BytesRead += count;
                return count;
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        [Test]
        public void Verify_that_ReadExactlyAsync_throws_when_stream_ends_early()
        {
            var reader = new ArgusWireReader(new MemoryStream(new byte[10]));

            Assert.That(
                async () => await reader.ReadExactlyAsync(new byte[20].AsMemory(), CancellationToken.None),
                Throws.TypeOf<EndOfStreamException>());
        }
    }
}
