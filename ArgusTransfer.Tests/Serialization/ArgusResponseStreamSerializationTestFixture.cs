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
            deserialized.BodyStream.ReadExactly(resultBytes, 0, resultBytes.Length);
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
    }
}
