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
    using System.Text;
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
    }
}
