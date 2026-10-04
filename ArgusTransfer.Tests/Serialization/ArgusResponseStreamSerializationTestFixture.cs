// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusResponseStreamSerializationTestFixture.cs">
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
    /// Suite of tests for the <see cref="ArgusResponseSerializer"/> class with streaming bodies
    /// </summary>
    [TestFixture]
    public class ArgusResponseStreamSerializationTestFixture
    {
        private ArgusResponseSerializer serializer;

        [SetUp]
        public void SetUp()
        {
            this.serializer = new ArgusResponseSerializer();
        }

        [Test]
        public void Verify_that_streamed_response_uses_transfer_encoding_header()
        {
            var response = new ArgusResponse
            {
                StatusCode = ArgusStatusCode.Ok,
                BodyStream = new MemoryStream(Encoding.UTF8.GetBytes("streamed response"))
            };

            var text = this.serializer.Write(response);

            Assert.That(text, Does.Contain("Transfer-Encoding: chunked\r\n"));
            Assert.That(text, Does.Not.Contain("Content-Length:"));
        }

        [Test]
        public void Verify_that_streamed_response_round_trips_via_string()
        {
            var bodyData = "Response body streamed via chunked encoding.";
            var response = new ArgusResponse
            {
                StatusCode = ArgusStatusCode.Ok,
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc),
                BodyStream = new MemoryStream(Encoding.UTF8.GetBytes(bodyData))
            };

            var text = this.serializer.Write(response);
            var deserialized = this.serializer.Read(text);

            Assert.That(deserialized.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(deserialized.CorrelationToken, Is.EqualTo(response.CorrelationToken));
            Assert.That(deserialized.IsStreamed, Is.True);
            Assert.That(deserialized.Body, Is.Null);

            var resultBytes = new byte[deserialized.BodyStream.Length];
            deserialized.BodyStream.ReadExactly(resultBytes, 0, resultBytes.Length);
            Assert.That(Encoding.UTF8.GetString(resultBytes), Is.EqualTo(bodyData));
        }

        [Test]
        public async Task Verify_that_streamed_response_round_trips_via_stream()
        {
            var bodyData = "Async response stream round-trip.";
            var response = new ArgusResponse
            {
                StatusCode = ArgusStatusCode.Created,
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc),
                BodyStream = new MemoryStream(Encoding.UTF8.GetBytes(bodyData))
            };

            var pipeStream = new MemoryStream();
            var writer = new StreamWriter(pipeStream, new UTF8Encoding(false)) { AutoFlush = false };

            await this.serializer.WriteAsync(writer, response);

            pipeStream.Position = 0;
            var reader = new StreamReader(pipeStream, new UTF8Encoding(false));

            var deserialized = await this.serializer.ReadAsync(reader, CancellationToken.None);

            Assert.That(deserialized.StatusCode, Is.EqualTo(ArgusStatusCode.Created));
            Assert.That(deserialized.IsStreamed, Is.True);

            var resultBytes = new byte[deserialized.BodyStream.Length];
            await deserialized.BodyStream.ReadExactlyAsync(resultBytes, 0, resultBytes.Length);
            Assert.That(Encoding.UTF8.GetString(resultBytes), Is.EqualTo(bodyData));
        }

        [Test]
        public void Verify_that_non_streamed_response_still_round_trips()
        {
            var response = new ArgusResponse
            {
                StatusCode = ArgusStatusCode.Ok,
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc),
                Body = "{\"result\":\"ok\"}"
            };

            var text = this.serializer.Write(response);
            var deserialized = this.serializer.Read(text);

            Assert.That(deserialized.Body, Is.EqualTo(response.Body));
            Assert.That(deserialized.IsStreamed, Is.False);
            Assert.That(text, Does.Contain("Content-Length:"));
            Assert.That(text, Does.Not.Contain("Transfer-Encoding:"));
        }

        [Test]
        public void Verify_that_streamed_response_defaults_content_type_to_octet_stream()
        {
            var response = new ArgusResponse
            {
                StatusCode = ArgusStatusCode.Ok,
                BodyStream = new MemoryStream(Encoding.UTF8.GetBytes("data"))
            };

            var text = this.serializer.Write(response);

            Assert.That(text, Does.Contain("Content-Type: application/octet-stream\r\n"));
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
            var response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = body };

            using var stream = new MemoryStream();
            await this.serializer.WriteAsync(stream, response);
            stream.Position = 0;

            var result = await this.serializer.ReadAsync(stream, CancellationToken.None);

            Assert.That(result.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(result.Body, Is.EqualTo(body));
        }

        [Test]
        public async Task Verify_that_binary_streamed_body_round_trips_via_Stream_overloads()
        {
            var payload = new byte[256];

            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)i;
            }

            var response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, BodyStream = new MemoryStream(payload) };

            using var stream = new MemoryStream();
            await this.serializer.WriteAsync(stream, response);
            stream.Position = 0;

            var result = await this.serializer.ReadAsync(stream, CancellationToken.None);

            Assert.That(ToArray(result.BodyStream), Is.EqualTo(payload));
        }

        [Test]
        public async Task Verify_that_Stream_overload_with_accept_sets_content_type_and_round_trips()
        {
            var response = new ArgusResponse { StatusCode = ArgusStatusCode.Ok, Body = "héllo" };

            using var stream = new MemoryStream();
            await this.serializer.WriteAsync(stream, response, "text/plain");
            stream.Position = 0;

            var result = await this.serializer.ReadAsync(stream, CancellationToken.None);

            Assert.That(result.Headers[ArgusHeaderNames.ContentType], Is.EqualTo("text/plain"));
            Assert.That(result.Body, Is.EqualTo("héllo"));
        }

        [Test]
        public void Verify_that_Stream_overload_throws_EndOfStreamException_for_empty_stream()
        {
            using var stream = new MemoryStream();

            Assert.That(async () => await this.serializer.ReadAsync(stream, CancellationToken.None), Throws.TypeOf<EndOfStreamException>());
        }

        [Test]
        public void Verify_that_Stream_overload_throws_FormatException_for_blank_status_line()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("   \r\n\r\n"));

            Assert.That(async () => await this.serializer.ReadAsync(stream, CancellationToken.None), Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Stream_overload_throws_EndOfStreamException_for_truncated_body()
        {
            var wire = "ARGUS/1.0 200 OK\r\nContent-Length: 50\r\n\r\npartial";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));

            Assert.That(async () => await this.serializer.ReadAsync(stream, CancellationToken.None), Throws.TypeOf<EndOfStreamException>());
        }

        [Test]
        public async Task Verify_that_StreamReader_overload_counts_Content_Length_in_bytes()
        {
            var wire = "ARGUS/1.0 200 OK\r\nContent-Length: 6\r\n\r\nhélloEXTRA";

            using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(wire)));
            var result = await this.serializer.ReadAsync(reader, CancellationToken.None);

            Assert.That(result.Body, Is.EqualTo("héllo"));
        }

        [Test]
        public void Verify_that_string_Read_counts_Content_Length_in_bytes()
        {
            var result = this.serializer.Read("ARGUS/1.0 200 OK\r\nContent-Length: 6\r\n\r\nhélloEXTRA");

            Assert.That(result.Body, Is.EqualTo("héllo"));
        }

        [Test]
        public void Verify_that_Stream_overloads_throw_for_null_arguments()
        {
            Assert.That(async () => await this.serializer.WriteAsync((Stream)null, new ArgusResponse()), Throws.TypeOf<ArgumentNullException>());
            Assert.That(async () => await this.serializer.WriteAsync(new MemoryStream(), (ArgusResponse)null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(async () => await this.serializer.ReadAsync((Stream)null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public async Task Verify_that_ReadAsync_with_maxBodySize_rejects_larger_bodies_on_both_overloads()
        {
            var wire = "ARGUS/1.0 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 20\r\n\r\n01234567890123456789";

            await Assert.ThatAsync(
                () => this.serializer.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(wire)), 10, CancellationToken.None),
                Throws.InstanceOf<ArgusProtocolException>());

            await Assert.ThatAsync(
                () => this.serializer.ReadAsync(new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(wire))), 10, CancellationToken.None),
                Throws.InstanceOf<ArgusProtocolException>());
        }

        [Test]
        public async Task Verify_that_ReadAsync_with_maxBodySize_accepts_bodies_within_the_limit()
        {
            var wire = "ARGUS/1.0 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 10\r\n\r\n0123456789";

            var fromStream = await this.serializer.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(wire)), 10, CancellationToken.None);
            var fromReader = await this.serializer.ReadAsync(new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(wire))), 10, CancellationToken.None);

            Assert.That(fromStream.Body, Is.EqualTo("0123456789"));
            Assert.That(fromReader.Body, Is.EqualTo("0123456789"));
        }
    }
}
