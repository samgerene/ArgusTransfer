// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusChunkedEncodingTestFixture.cs">
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

    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusChunkedEncoding"/> class
    /// </summary>
    [TestFixture]
    public class ArgusChunkedEncodingTestFixture
    {
        [Test]
        public void Verify_that_chunked_write_produces_correct_wire_format()
        {
            var data = Encoding.UTF8.GetBytes("Hello, chunked world!");
            var source = new MemoryStream(data);
            var sb = new StringBuilder();

            ArgusChunkedEncoding.WriteChunked(source, sb, chunkSize: 10);

            var wire = sb.ToString();

            Assert.That(wire, Does.StartWith("a\r\n"));
            Assert.That(wire, Does.EndWith("0\r\n\r\n"));
        }

        [Test]
        public void Verify_that_chunked_round_trip_preserves_data()
        {
            var original = "The quick brown fox jumps over the lazy dog.";
            var data = Encoding.UTF8.GetBytes(original);
            var source = new MemoryStream(data);
            var sb = new StringBuilder();

            ArgusChunkedEncoding.WriteChunked(source, sb, chunkSize: 8);

            var reader = new StringReader(sb.ToString());
            var result = ArgusChunkedEncoding.ReadChunked(reader);

            var decoded = Encoding.UTF8.GetString(result.ToArray());
            Assert.That(decoded, Is.EqualTo(original));
        }

        [Test]
        public void Verify_that_empty_stream_produces_only_terminator()
        {
            var source = new MemoryStream();
            var sb = new StringBuilder();

            ArgusChunkedEncoding.WriteChunked(source, sb);

            Assert.That(sb.ToString(), Is.EqualTo("0\r\n\r\n"));
        }

        [Test]
        public void Verify_that_read_throws_when_exceeding_max_body_size()
        {
            var data = Encoding.UTF8.GetBytes("This payload is too large for the limit");
            var source = new MemoryStream(data);
            var sb = new StringBuilder();

            ArgusChunkedEncoding.WriteChunked(source, sb);

            var reader = new StringReader(sb.ToString());

            Assert.That(
                () => ArgusChunkedEncoding.ReadChunked(reader, maxBodySize: 10),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("maximum allowed size"));
        }

        [Test]
        public async System.Threading.Tasks.Task Verify_that_async_chunked_round_trip_preserves_data()
        {
            var original = "Async streaming data payload for testing.";
            var data = Encoding.UTF8.GetBytes(original);

            var pipeStream = new MemoryStream();
            var writer = new StreamWriter(pipeStream, new UTF8Encoding(false)) { AutoFlush = false };

            await ArgusChunkedEncoding.WriteChunkedAsync(new MemoryStream(data), writer, chunkSize: 12);

            pipeStream.Position = 0;
            var reader = new StreamReader(pipeStream, new UTF8Encoding(false));

            var result = await ArgusChunkedEncoding.ReadChunkedAsync(reader);

            var decoded = Encoding.UTF8.GetString(result.ToArray());
            Assert.That(decoded, Is.EqualTo(original));
        }

        [Test]
        public void Verify_that_single_chunk_round_trips()
        {
            var original = "small";
            var data = Encoding.UTF8.GetBytes(original);
            var source = new MemoryStream(data);
            var sb = new StringBuilder();

            ArgusChunkedEncoding.WriteChunked(source, sb, chunkSize: 8192);

            var wire = sb.ToString();

            Assert.That(wire, Is.EqualTo("5\r\nsmall\r\n0\r\n\r\n"));

            var reader = new StringReader(wire);
            var result = ArgusChunkedEncoding.ReadChunked(reader);

            Assert.That(Encoding.UTF8.GetString(result.ToArray()), Is.EqualTo(original));
        }
    }
}
