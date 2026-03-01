// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRequestSerializationTestFixture.cs" company="Sam Gerené">
//
//     Copyright (c) 2026 Sam Gerené
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

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusRequestWriter"/> and <see cref="ArgusRequestReader"/> classes
    /// </summary>
    [TestFixture]
    public class ArgusRequestSerializationTestFixture
    {
        [Test]
        public void Verify_that_request_round_trips_with_body()
        {
            var original = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/healthendpoint",
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc),
                Body = "{\"name\":\"test\",\"url\":\"https://example.com\"}"
            };

            var text = ArgusRequestWriter.Write(original);
            var deserialized = ArgusRequestReader.Read(text);

            Assert.That(deserialized.Verb, Is.EqualTo(original.Verb));
            Assert.That(deserialized.Route, Is.EqualTo(original.Route));
            Assert.That(deserialized.CorrelationToken, Is.EqualTo(original.CorrelationToken));
            Assert.That(deserialized.Timestamp, Is.EqualTo(original.Timestamp));
            Assert.That(deserialized.Body, Is.EqualTo(original.Body));
        }

        [Test]
        public void Verify_that_request_round_trips_without_body()
        {
            var original = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/healthendpoint",
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc)
            };

            var text = ArgusRequestWriter.Write(original);
            var deserialized = ArgusRequestReader.Read(text);

            Assert.That(deserialized.Verb, Is.EqualTo(original.Verb));
            Assert.That(deserialized.Route, Is.EqualTo(original.Route));
            Assert.That(deserialized.CorrelationToken, Is.EqualTo(original.CorrelationToken));
            Assert.That(deserialized.Timestamp, Is.EqualTo(original.Timestamp));
            Assert.That(deserialized.Body, Is.Null);
        }

        [Test]
        public void Verify_that_request_line_format_is_correct()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/healthendpoint",
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc)
            };

            var text = ArgusRequestWriter.Write(request);

            Assert.That(text, Does.StartWith("POST /healthendpoint ARGUS/1.0\r\n"));
        }

        [Test]
        public void Verify_that_headers_are_written_and_read()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/healthendpoint",
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc)
            };

            var text = ArgusRequestWriter.Write(request);

            Assert.That(text, Does.Contain("X-Correlation-Token: cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e\r\n"));
            Assert.That(text, Does.Contain("X-Timestamp: 2026-02-28T14:30:00.0000000Z\r\n"));
        }

        [Test]
        public void Verify_that_custom_headers_round_trip()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/healthendpoint",
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc)
            };

            request.Headers["X-Custom"] = "my-value";

            var text = ArgusRequestWriter.Write(request);
            var deserialized = ArgusRequestReader.Read(text);

            Assert.That(deserialized.Headers["X-Custom"], Is.EqualTo("my-value"));
        }

        [Test]
        public void Verify_that_content_length_is_written_for_body()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/healthendpoint",
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc),
                Body = "{\"name\":\"test\"}"
            };

            var text = ArgusRequestWriter.Write(request);

            Assert.That(text, Does.Contain("Content-Length:"));
            Assert.That(text, Does.Contain("Content-Type: application/json\r\n"));
        }
    }
}
