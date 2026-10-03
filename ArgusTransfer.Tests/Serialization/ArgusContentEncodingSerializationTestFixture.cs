// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusContentEncodingSerializationTestFixture.cs">
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
    /// Suite of tests for <c>Content-Encoding</c> support in <see cref="ArgusRequestSerializer"/> and <see cref="ArgusResponseSerializer"/>
    /// </summary>
    [TestFixture]
    public class ArgusContentEncodingSerializationTestFixture
    {
        private static readonly string LargeText = string.Concat(Enumerable.Repeat("The quick brown fox jumps over the lazy dög. ", 500));

        private ArgusRequestSerializer requestSerializer;

        private ArgusResponseSerializer responseSerializer;

        [SetUp]
        public void SetUp()
        {
            this.requestSerializer = new ArgusRequestSerializer();
            this.responseSerializer = new ArgusResponseSerializer();
        }

        private static ArgusRequest CreateRequest(string body, string contentEncoding = "gzip")
        {
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/items", Body = body };
            request.Headers[ArgusHeaderNames.ContentEncoding] = contentEncoding;
            return request;
        }

        private static (string Head, byte[] Body) SplitWire(byte[] wire)
        {
            var separator = Encoding.ASCII.GetBytes("\r\n\r\n");

            for (var i = 0; i <= wire.Length - separator.Length; i++)
            {
                if (wire.AsSpan(i, separator.Length).SequenceEqual(separator))
                {
                    return (Encoding.UTF8.GetString(wire, 0, i), wire[(i + separator.Length)..]);
                }
            }

            throw new InvalidOperationException("No header terminator found.");
        }

        [Test]
        public async Task Verify_that_gzip_string_body_is_compressed_on_the_wire_and_round_trips()
        {
            using var stream = new MemoryStream();
            await this.requestSerializer.WriteAsync(stream, CreateRequest(LargeText));

            var (head, body) = SplitWire(stream.ToArray());

            Assert.That(head, Does.Contain("Content-Encoding: gzip"));
            Assert.That(head, Does.Contain($"Content-Length: {body.Length}"));
            Assert.That(body[0], Is.EqualTo(0x1F));
            Assert.That(body[1], Is.EqualTo(0x8B));
            Assert.That(body.Length, Is.LessThan(Encoding.UTF8.GetByteCount(LargeText) / 10));

            stream.Position = 0;
            var result = await this.requestSerializer.ReadAsync(stream, CancellationToken.None);

            Assert.That(result.Body, Is.EqualTo(LargeText));
            Assert.That(result.Headers.ContainsKey(ArgusHeaderNames.ContentEncoding), Is.False);
        }

        [Test]
        public async Task Verify_that_gzip_streamed_body_round_trips()
        {
            var payload = Encoding.UTF8.GetBytes(LargeText);
            var response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, BodyStream = new MemoryStream(payload) };
            response.Headers[ArgusHeaderNames.ContentEncoding] = "gzip";

            using var stream = new MemoryStream();
            await this.responseSerializer.WriteAsync(stream, response);

            Assert.That(stream.Length, Is.LessThan(payload.Length / 10));

            stream.Position = 0;
            var result = await this.responseSerializer.ReadAsync(stream, CancellationToken.None);

            using var decoded = new MemoryStream();
            await result.BodyStream.CopyToAsync(decoded);

            Assert.That(decoded.ToArray(), Is.EqualTo(payload));
            Assert.That(result.Headers.ContainsKey(ArgusHeaderNames.ContentEncoding), Is.False);
        }

        [Test]
        public async Task Verify_that_gzip_response_with_accept_content_type_round_trips()
        {
            var response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = LargeText };
            response.Headers[ArgusHeaderNames.ContentEncoding] = "gzip";

            using var stream = new MemoryStream();
            await this.responseSerializer.WriteAsync(stream, response, "text/plain");
            stream.Position = 0;

            var result = await this.responseSerializer.ReadAsync(stream, CancellationToken.None);

            Assert.That(result.Body, Is.EqualTo(LargeText));
            Assert.That(result.Headers[ArgusHeaderNames.ContentType], Is.EqualTo("text/plain"));
        }

        [Test]
        public async Task Verify_that_identity_encoding_is_not_compressed()
        {
            using var stream = new MemoryStream();
            await this.requestSerializer.WriteAsync(stream, CreateRequest("plain body", "identity"));

            var (_, body) = SplitWire(stream.ToArray());

            Assert.That(Encoding.UTF8.GetString(body), Is.EqualTo("plain body"));
        }

        [Test]
        public void Verify_that_writing_an_unsupported_encoding_throws()
        {
            using var stream = new MemoryStream();

            Assert.That(
                async () => await this.requestSerializer.WriteAsync(stream, CreateRequest(LargeText, "br")),
                Throws.TypeOf<InvalidOperationException>().With.Message.Contains("br"));
        }

        [Test]
        public void Verify_that_reading_an_unsupported_encoding_throws()
        {
            var wire = "POST /items ARGUS/1.0\r\nContent-Encoding: br\r\nContent-Length: 3\r\n\r\nabc";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));

            Assert.That(
                async () => await this.requestSerializer.ReadAsync(stream, CancellationToken.None),
                Throws.TypeOf<ArgusProtocolException>().With.Message.Contains("br"));
        }

        [Test]
        public async Task Verify_that_decompressed_size_is_limited_by_max_body_size()
        {
            var bomb = new string('a', 200_000);

            using var stream = new MemoryStream();
            await this.requestSerializer.WriteAsync(stream, CreateRequest(bomb));

            Assert.That(stream.Length, Is.LessThan(5_000), "the compressed request fits within the limit");

            stream.Position = 0;

            Assert.That(
                async () => await this.requestSerializer.ReadAsync(stream, CancellationToken.None, maxBodySize: 10_000),
                Throws.TypeOf<ArgusProtocolException>().With.Message.Contains("Decompressed body size"));
        }

        [Test]
        public void Verify_that_text_overloads_reject_content_encoding()
        {
            var compressedWire = "POST /items ARGUS/1.0\r\nContent-Encoding: gzip\r\nContent-Length: 3\r\n\r\nabc";

            Assert.That(() => this.requestSerializer.Write(CreateRequest("body")), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => this.requestSerializer.Read(compressedWire), Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                async () => await this.requestSerializer.ReadAsync(new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(compressedWire))), CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public async Task Verify_that_custom_content_encoding_can_be_registered()
        {
            this.requestSerializer.ContentEncodings.Add(new ArgusCompressionTestFixture.DeflateTestEncoding());

            using var stream = new MemoryStream();
            await this.requestSerializer.WriteAsync(stream, CreateRequest(LargeText, "deflate"));
            stream.Position = 0;

            var result = await this.requestSerializer.ReadAsync(stream, CancellationToken.None);

            Assert.That(result.Body, Is.EqualTo(LargeText));
        }

        [Test]
        public void Verify_that_ContentEncodings_defaults_to_gzip_and_rejects_null()
        {
            Assert.That(this.requestSerializer.ContentEncodings.Single(), Is.TypeOf<GZipArgusContentEncoding>());
            Assert.That(this.responseSerializer.ContentEncodings.Single(), Is.TypeOf<GZipArgusContentEncoding>());
            Assert.That(() => this.requestSerializer.ContentEncodings = null, Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => this.responseSerializer.ContentEncodings = null, Throws.TypeOf<ArgumentNullException>());
        }
    }
}
