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
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("maximum allowed size"));
        }
    }
}
