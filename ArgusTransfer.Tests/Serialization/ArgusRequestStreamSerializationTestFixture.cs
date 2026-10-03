// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRequestStreamSerializationTestFixture.cs">
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
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusRequestSerializer"/> class with streaming bodies
    /// </summary>
    [TestFixture]
    public class ArgusRequestStreamSerializationTestFixture
    {
        private ArgusRequestSerializer serializer;

        [SetUp]
        public void SetUp()
        {
            this.serializer = new ArgusRequestSerializer();
        }

        [Test]
        public void Verify_that_streamed_request_uses_transfer_encoding_header()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/upload",
                BodyStream = new MemoryStream(Encoding.UTF8.GetBytes("streamed content"))
            };

            var text = this.serializer.Write(request);

            Assert.That(text, Does.Contain("Transfer-Encoding: chunked\r\n"));
            Assert.That(text, Does.Not.Contain("Content-Length:"));
        }

        [Test]
        public void Verify_that_streamed_request_round_trips_via_string()
        {
            var bodyData = "The payload to be chunked and reassembled.";
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.PUT,
                Route = "/data",
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc),
                BodyStream = new MemoryStream(Encoding.UTF8.GetBytes(bodyData))
            };

            var text = this.serializer.Write(request);
            var deserialized = this.serializer.Read(text);

            Assert.That(deserialized.Verb, Is.EqualTo(ArgusVerb.PUT));
            Assert.That(deserialized.Route, Is.EqualTo("/data"));
            Assert.That(deserialized.CorrelationToken, Is.EqualTo(request.CorrelationToken));
            Assert.That(deserialized.IsStreamed, Is.True);
            Assert.That(deserialized.Body, Is.Null);

            var resultBytes = new byte[deserialized.BodyStream.Length];
            deserialized.BodyStream.ReadExactly(resultBytes, 0, resultBytes.Length);
            Assert.That(Encoding.UTF8.GetString(resultBytes), Is.EqualTo(bodyData));
        }

        [Test]
        public async Task Verify_that_streamed_request_round_trips_via_stream()
        {
            var bodyData = "Async stream round-trip test content.";
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/stream-test",
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc),
                BodyStream = new MemoryStream(Encoding.UTF8.GetBytes(bodyData))
            };

            var pipeStream = new MemoryStream();
            var writer = new StreamWriter(pipeStream, new UTF8Encoding(false)) { AutoFlush = false };

            await this.serializer.WriteAsync(writer, request);

            pipeStream.Position = 0;
            var reader = new StreamReader(pipeStream, new UTF8Encoding(false));

            var deserialized = await this.serializer.ReadAsync(reader, CancellationToken.None);

            Assert.That(deserialized.Verb, Is.EqualTo(ArgusVerb.POST));
            Assert.That(deserialized.Route, Is.EqualTo("/stream-test"));
            Assert.That(deserialized.IsStreamed, Is.True);

            var resultBytes = new byte[deserialized.BodyStream.Length];
            await deserialized.BodyStream.ReadExactlyAsync(resultBytes, 0, resultBytes.Length);
            Assert.That(Encoding.UTF8.GetString(resultBytes), Is.EqualTo(bodyData));
        }

        [Test]
        public void Verify_that_non_streamed_request_still_round_trips()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/legacy",
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc),
                Body = "{\"key\":\"value\"}"
            };

            var text = this.serializer.Write(request);
            var deserialized = this.serializer.Read(text);

            Assert.That(deserialized.Body, Is.EqualTo(request.Body));
            Assert.That(deserialized.IsStreamed, Is.False);
            Assert.That(text, Does.Contain("Content-Length:"));
            Assert.That(text, Does.Not.Contain("Transfer-Encoding:"));
        }

        [Test]
        public void Verify_that_streamed_request_includes_content_type_when_specified()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/upload",
                BodyStream = new MemoryStream(Encoding.UTF8.GetBytes("data"))
            };

            request.Headers["Content-Type"] = "application/json";

            var text = this.serializer.Write(request);

            Assert.That(text, Does.Contain("Content-Type: application/json\r\n"));
            Assert.That(text, Does.Contain("Transfer-Encoding: chunked\r\n"));
        }

        [Test]
        public void Verify_that_streamed_request_defaults_content_type_to_octet_stream()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/upload",
                BodyStream = new MemoryStream(Encoding.UTF8.GetBytes("data"))
            };

            var text = this.serializer.Write(request);

            Assert.That(text, Does.Contain("Content-Type: application/octet-stream\r\n"));
        }

        [Test]
        public void Verify_that_chunked_body_size_enforcement_works()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/upload",
                BodyStream = new MemoryStream(Encoding.UTF8.GetBytes("this payload exceeds the limit"))
            };

            var text = this.serializer.Write(request);

            Assert.That(
                () => this.serializer.Read(text, maxBodySize: 5),
                Throws.TypeOf<ArgusProtocolException>()
                    .With.Message.Contains("maximum allowed size"));
        }

        private static byte[] AllByteValues()
        {
            var bytes = new byte[256];

            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)i;
            }

            return bytes;
        }

        private static byte[] ToArray(Stream stream)
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }

        [Test]
        public async Task Verify_that_non_ascii_string_body_round_trips_via_Stream_overloads()
        {
            const string body = "héllo wörld € 😀";
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/echo", Body = body };

            using var stream = new MemoryStream();
            await this.serializer.WriteAsync(stream, request);

            var wire = Encoding.UTF8.GetString(stream.ToArray());
            Assert.That(wire, Does.Contain($"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n"));

            stream.Position = 0;
            var result = await this.serializer.ReadAsync(stream, CancellationToken.None);

            Assert.That(result.Body, Is.EqualTo(body));
        }

        [Test]
        public async Task Verify_that_binary_streamed_body_round_trips_via_Stream_overloads()
        {
            var payload = AllByteValues();
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/upload", BodyStream = new MemoryStream(payload) };

            using var stream = new MemoryStream();
            await this.serializer.WriteAsync(stream, request);
            stream.Position = 0;

            var result = await this.serializer.ReadAsync(stream, CancellationToken.None);

            Assert.That(ToArray(result.BodyStream), Is.EqualTo(payload));
        }

        [Test]
        public async Task Verify_that_large_binary_streamed_body_spanning_many_chunks_round_trips()
        {
            var payload = new byte[100_000];
            new Random(7).NextBytes(payload);
            var request = new ArgusRequest { Verb = ArgusVerb.PUT, Route = "/upload", BodyStream = new MemoryStream(payload) };

            using var stream = new MemoryStream();
            await this.serializer.WriteAsync(stream, request);
            stream.Position = 0;

            var result = await this.serializer.ReadAsync(stream, CancellationToken.None);

            Assert.That(ToArray(result.BodyStream), Is.EqualTo(payload));
        }

        [Test]
        public async Task Verify_that_multibyte_text_split_across_chunk_boundaries_round_trips()
        {
            // 8191 ASCII bytes followed by multi-byte characters forces a character to straddle the 8 KiB chunk boundary
            var text = new string('a', 8191) + "€€€ héllo";
            var payload = Encoding.UTF8.GetBytes(text);
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/upload", BodyStream = new MemoryStream(payload) };

            using var stream = new MemoryStream();
            await this.serializer.WriteAsync(stream, request);
            stream.Position = 0;

            var result = await this.serializer.ReadAsync(stream, CancellationToken.None);

            Assert.That(Encoding.UTF8.GetString(ToArray(result.BodyStream)), Is.EqualTo(text));
        }

        [Test]
        public async Task Verify_that_StreamWriter_overload_writes_binary_streamed_body_byte_safe()
        {
            var payload = AllByteValues();
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/upload", BodyStream = new MemoryStream(payload) };

            using var stream = new MemoryStream();
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true))
            {
                await this.serializer.WriteAsync(writer, request);
            }

            stream.Position = 0;
            var result = await this.serializer.ReadAsync(stream, CancellationToken.None);

            Assert.That(ToArray(result.BodyStream), Is.EqualTo(payload));
        }

        [Test]
        public async Task Verify_that_Stream_overload_accepts_LF_only_line_endings()
        {
            var wire = "POST /echo ARGUS/1.0\nContent-Length: 5\n\nhello";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));
            var result = await this.serializer.ReadAsync(stream, CancellationToken.None);

            Assert.That(result.Verb, Is.EqualTo(ArgusVerb.POST));
            Assert.That(result.Body, Is.EqualTo("hello"));
        }

        [Test]
        public void Verify_that_Stream_overload_throws_EndOfStreamException_for_truncated_body()
        {
            var wire = "POST /echo ARGUS/1.0\r\nContent-Length: 50\r\n\r\nonly part of the body";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));

            Assert.That(async () => await this.serializer.ReadAsync(stream, CancellationToken.None), Throws.TypeOf<EndOfStreamException>());
        }

        [Test]
        public void Verify_that_Stream_overload_throws_EndOfStreamException_for_truncated_chunked_body()
        {
            var wire = "POST /upload ARGUS/1.0\r\nTransfer-Encoding: chunked\r\n\r\n10\r\nshort";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));

            Assert.That(async () => await this.serializer.ReadAsync(stream, CancellationToken.None), Throws.TypeOf<EndOfStreamException>());
        }

        [Test]
        public void Verify_that_Stream_overload_throws_FormatException_for_chunk_without_terminator()
        {
            var wire = "POST /upload ARGUS/1.0\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nabcXYZ\r\n0\r\n\r\n";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));

            Assert.That(async () => await this.serializer.ReadAsync(stream, CancellationToken.None), Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Stream_overload_throws_FormatException_for_empty_stream()
        {
            using var stream = new MemoryStream();

            Assert.That(async () => await this.serializer.ReadAsync(stream, CancellationToken.None), Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Stream_overload_enforces_max_body_size_for_Content_Length()
        {
            var wire = "POST /echo ARGUS/1.0\r\nContent-Length: 100\r\n\r\n";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));

            Assert.That(
                async () => await this.serializer.ReadAsync(stream, CancellationToken.None, maxBodySize: 10),
                Throws.TypeOf<ArgusProtocolException>().With.Message.Contains("maximum allowed size"));
        }

        [Test]
        public async Task Verify_that_Stream_overload_enforces_max_body_size_for_chunked_body()
        {
            var request = new ArgusRequest { Verb = ArgusVerb.POST, Route = "/upload", BodyStream = new MemoryStream(new byte[100]) };

            using var stream = new MemoryStream();
            await this.serializer.WriteAsync(stream, request);
            stream.Position = 0;

            Assert.That(
                async () => await this.serializer.ReadAsync(stream, CancellationToken.None, maxBodySize: 10),
                Throws.TypeOf<ArgusProtocolException>().With.Message.Contains("maximum allowed size"));
        }

        [Test]
        public void Verify_that_MaxHeaderSize_defaults_to_32_KB_and_rejects_invalid_values()
        {
            Assert.That(this.serializer.MaxHeaderSize, Is.EqualTo(32 * 1024));
            Assert.That(() => this.serializer.MaxHeaderSize = 0, Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => this.serializer.MaxHeaderSize = -1, Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Verify_that_Stream_overload_rejects_an_over_long_header_line()
        {
            var wire = "GET /items ARGUS/1.0\r\nX-Big: " + new string('x', 40 * 1024) + "\r\n\r\n";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));

            Assert.That(
                async () => await this.serializer.ReadAsync(stream, CancellationToken.None),
                Throws.TypeOf<ArgusProtocolException>().With.Message.Contains("header block exceeds the maximum allowed size of 32768 bytes"));
        }

        [Test]
        public void Verify_that_Stream_overload_rejects_too_many_headers()
        {
            var headers = new StringBuilder("GET /items ARGUS/1.0\r\n");

            for (var i = 0; i < 2_000; i++)
            {
                headers.Append("X-Header-").Append(i).Append(": value\r\n");
            }

            headers.Append("\r\n");
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(headers.ToString()));

            Assert.That(async () => await this.serializer.ReadAsync(stream, CancellationToken.None), Throws.TypeOf<ArgusProtocolException>());
        }

        [Test]
        public void Verify_that_Stream_overload_counts_the_request_line_towards_the_limit()
        {
            this.serializer.MaxHeaderSize = 64;
            var wire = "GET /" + new string('a', 100) + " ARGUS/1.0\r\n\r\n";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));

            Assert.That(async () => await this.serializer.ReadAsync(stream, CancellationToken.None), Throws.TypeOf<ArgusProtocolException>());
        }

        [Test]
        public async Task Verify_that_Stream_overload_accepts_headers_within_a_custom_limit()
        {
            this.serializer.MaxHeaderSize = 256;
            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/items" };
            request.Headers["X-Small"] = "value";

            using var stream = new MemoryStream();
            await this.serializer.WriteAsync(stream, request);
            stream.Position = 0;

            var result = await this.serializer.ReadAsync(stream, CancellationToken.None);

            Assert.That(result.Headers["X-Small"], Is.EqualTo("value"));
        }

        [Test]
        public void Verify_that_StreamReader_overload_rejects_an_over_long_header_line()
        {
            this.serializer.MaxHeaderSize = 1024;
            var wire = "GET /items ARGUS/1.0\r\nX-Big: " + new string('x', 2048) + "\r\n\r\n";
            using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(wire)));

            Assert.That(async () => await this.serializer.ReadAsync(reader, CancellationToken.None), Throws.TypeOf<ArgusProtocolException>());
        }

        [Test]
        public void Verify_that_Stream_overload_rejects_an_over_long_chunk_size_line()
        {
            var wire = "POST /upload ARGUS/1.0\r\nTransfer-Encoding: chunked\r\n\r\n" + new string('0', 4096) + "\r\n";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));

            Assert.That(async () => await this.serializer.ReadAsync(stream, CancellationToken.None), Throws.InstanceOf<ArgusProtocolException>());
        }

        [Test]
        public async Task Verify_that_StreamReader_overload_counts_Content_Length_in_bytes()
        {
            // "héllo" is 5 characters but 6 bytes; the trailing data must not be read into the body
            var wire = "POST /echo ARGUS/1.0\r\nContent-Length: 6\r\n\r\nhélloEXTRA";

            using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(wire)));
            var result = await this.serializer.ReadAsync(reader, CancellationToken.None);

            Assert.That(result.Body, Is.EqualTo("héllo"));
        }

        [Test]
        public void Verify_that_string_Read_counts_Content_Length_in_bytes()
        {
            var wire = "POST /echo ARGUS/1.0\r\nContent-Length: 6\r\n\r\nhélloEXTRA";

            var result = this.serializer.Read(wire);

            Assert.That(result.Body, Is.EqualTo("héllo"));
        }

        [Test]
        public void Verify_that_Stream_overloads_throw_for_null_arguments()
        {
            Assert.That(async () => await this.serializer.WriteAsync((Stream)null, new ArgusRequest()), Throws.TypeOf<ArgumentNullException>());
            Assert.That(async () => await this.serializer.WriteAsync(new MemoryStream(), null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(async () => await this.serializer.ReadAsync((Stream)null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
        }
    }
}
