// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusResponseSerializationTestFixture.cs">
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
    /// Suite of tests for the <see cref="ArgusTextResponseSerializer"/> class (round-trip)
    /// </summary>
    [TestFixture]
    public class ArgusResponseSerializationTestFixture
    {
        private ArgusTextResponseSerializer serializer;

        [SetUp]
        public void SetUp()
        {
            this.serializer = new ArgusTextResponseSerializer();
        }

        [Test]
        public void Verify_that_response_round_trips_with_body()
        {
            var original = new ArgusResponse
            {
                StatusCode = ArgusStatusCode.Created,
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc),
                Body = "{\"name\":\"test\",\"url\":\"https://example.com\"}"
            };

            var text = this.serializer.Write(original);
            var deserialized = this.serializer.Read(text);

            Assert.That(deserialized.StatusCode, Is.EqualTo(original.StatusCode));
            Assert.That(deserialized.CorrelationToken, Is.EqualTo(original.CorrelationToken));
            Assert.That(deserialized.Timestamp, Is.EqualTo(original.Timestamp));
            Assert.That(deserialized.Body, Is.EqualTo(original.Body));
        }

        [Test]
        public void Verify_that_response_round_trips_without_body()
        {
            var original = new ArgusResponse
            {
                StatusCode = ArgusStatusCode.NotFound,
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc)
            };

            var text = this.serializer.Write(original);
            var deserialized = this.serializer.Read(text);

            Assert.That(deserialized.StatusCode, Is.EqualTo(original.StatusCode));
            Assert.That(deserialized.CorrelationToken, Is.EqualTo(original.CorrelationToken));
            Assert.That(deserialized.Timestamp, Is.EqualTo(original.Timestamp));
            Assert.That(deserialized.Body, Is.Null);
        }

        [Test]
        public void Verify_that_status_line_format_is_correct()
        {
            var response = new ArgusResponse
            {
                StatusCode = ArgusStatusCode.Ok,
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc)
            };

            var text = this.serializer.Write(response);

            Assert.That(text, Does.StartWith("ARGUS/1.0 200 OK\r\n"));
        }

        [Test]
        public void Verify_that_all_status_codes_produce_correct_status_lines()
        {
            var testCases = new[]
            {
                (ArgusStatusCode.Ok, "ARGUS/1.0 200 OK\r\n"),
                (ArgusStatusCode.Created, "ARGUS/1.0 201 Created\r\n"),
                (ArgusStatusCode.BadRequest, "ARGUS/1.0 400 Bad Request\r\n"),
                (ArgusStatusCode.NotFound, "ARGUS/1.0 404 Not Found\r\n"),
                (ArgusStatusCode.InternalServerError, "ARGUS/1.0 500 Internal Server Error\r\n")
            };

            foreach (var (statusCode, expectedLine) in testCases)
            {
                var response = new ArgusResponse
                {
                    StatusCode = statusCode,
                    Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc)
                };

                var text = this.serializer.Write(response);

                Assert.That(text, Does.StartWith(expectedLine),
                    $"Status code {statusCode} should produce line starting with '{expectedLine}'");
            }
        }

        [Test]
        public void Verify_that_content_length_is_written_for_body()
        {
            var response = new ArgusResponse
            {
                StatusCode = ArgusStatusCode.Ok,
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc),
                Body = "{\"name\":\"test\"}"
            };

            var text = this.serializer.Write(response);

            Assert.That(text, Does.Contain("Content-Length:"));
            Assert.That(text, Does.Contain("Content-Type: application/json\r\n"));
        }

        [Test]
        public void Verify_that_no_content_headers_when_no_body()
        {
            var response = new ArgusResponse
            {
                StatusCode = ArgusStatusCode.NotFound,
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc)
            };

            var text = this.serializer.Write(response);

            Assert.That(text, Does.Not.Contain("Content-Length"));
            Assert.That(text, Does.Not.Contain("Content-Type"));
        }
    }
}
