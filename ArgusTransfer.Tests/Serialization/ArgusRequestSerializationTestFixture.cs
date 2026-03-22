// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRequestSerializationTestFixture.cs">
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

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusRequestSerializer"/> class (round-trip)
    /// </summary>
    [TestFixture]
    public class ArgusRequestSerializationTestFixture
    {
        private ArgusRequestSerializer serializer;

        [SetUp]
        public void SetUp()
        {
            this.serializer = new ArgusRequestSerializer();
        }

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

            var text = this.serializer.Write(original);
            var deserialized = this.serializer.Read(text);

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

            var text = this.serializer.Write(original);
            var deserialized = this.serializer.Read(text);

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

            var text = this.serializer.Write(request);

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

            var text = this.serializer.Write(request);

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

            var text = this.serializer.Write(request);
            var deserialized = this.serializer.Read(text);

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

            var text = this.serializer.Write(request);

            Assert.That(text, Does.Contain("Content-Length:"));
            Assert.That(text, Does.Contain("Content-Type: text/plain\r\n"));
        }

        [Test]
        public void Verify_that_request_round_trips_with_query_parameters()
        {
            var original = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/items",
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc)
            };

            original.QueryParameters["page"] = "1";
            original.QueryParameters["size"] = "10";

            var text = this.serializer.Write(original);
            var deserialized = this.serializer.Read(text);

            Assert.That(deserialized.Route, Is.EqualTo("/items"));
            Assert.That(deserialized.QueryParameters["page"], Is.EqualTo("1"));
            Assert.That(deserialized.QueryParameters["size"], Is.EqualTo("10"));
        }

        [Test]
        public void Verify_that_request_line_includes_query_string()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/items",
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc)
            };

            request.QueryParameters["page"] = "1";

            var text = this.serializer.Write(request);

            Assert.That(text, Does.StartWith("GET /items?page=1 ARGUS/1.0\r\n"));
        }

        [Test]
        public void Verify_that_no_query_params_produces_empty_dictionary()
        {
            var original = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/items",
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc)
            };

            var text = this.serializer.Write(original);
            var deserialized = this.serializer.Read(text);

            Assert.That(deserialized.QueryParameters.Count, Is.EqualTo(0));
        }

        [Test]
        public void Verify_that_special_characters_in_query_params_round_trip()
        {
            var original = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/search",
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc)
            };

            original.QueryParameters["q"] = "hello world";
            original.QueryParameters["filter"] = "a&b=c";

            var text = this.serializer.Write(original);
            var deserialized = this.serializer.Read(text);

            Assert.That(deserialized.QueryParameters["q"], Is.EqualTo("hello world"));
            Assert.That(deserialized.QueryParameters["filter"], Is.EqualTo("a&b=c"));
        }

        [Test]
        public void Verify_that_Accept_header_round_trips()
        {
            var original = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/healthendpoint",
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc),
                Accept = "application/json"
            };

            var text = this.serializer.Write(original);
            var deserialized = this.serializer.Read(text);

            Assert.That(deserialized.Accept, Is.EqualTo("application/json"));
        }

        [Test]
        public void Verify_that_Accept_header_is_written_to_wire_format()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/healthendpoint",
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc),
                Accept = "text/xml"
            };

            var text = this.serializer.Write(request);

            Assert.That(text, Does.Contain("Accept: text/xml\r\n"));
        }

        [Test]
        public void Verify_that_Accept_defaults_to_text_plain()
        {
            var request = new ArgusRequest();

            Assert.That(request.Accept, Is.EqualTo("text/plain"));
        }

        [Test]
        public void Verify_that_default_Accept_header_appears_on_wire()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/healthendpoint",
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc)
            };

            var text = this.serializer.Write(request);

            Assert.That(text, Does.Contain("Accept: text/plain\r\n"));
        }

        [Test]
        public void Verify_that_Accept_header_is_omitted_when_cleared()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/healthendpoint",
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc),
                Accept = null
            };

            var text = this.serializer.Write(request);

            Assert.That(text, Does.Not.Contain("Accept:"));
        }

        [Test]
        public void Verify_that_Accept_property_and_Headers_are_in_sync()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/healthendpoint",
                Accept = "application/json"
            };

            Assert.That(request.Headers["Accept"], Is.EqualTo("application/json"));

            request.Accept = null;

            Assert.That(request.Headers.ContainsKey("Accept"), Is.False);
        }

        [Test]
        public void Verify_that_Accept_set_via_Headers_is_read_via_property()
        {
            var request = new ArgusRequest
            {
                Verb = ArgusVerb.GET,
                Route = "/healthendpoint"
            };

            request.Headers["Accept"] = "text/plain";

            Assert.That(request.Accept, Is.EqualTo("text/plain"));
        }

        [Test]
        public void Verify_that_Content_Type_round_trips()
        {
            var original = new ArgusRequest
            {
                Verb = ArgusVerb.POST,
                Route = "/healthendpoint",
                CorrelationToken = Guid.Parse("cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e"),
                Timestamp = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc),
                Body = "{\"name\":\"test\"}"
            };

            var text = this.serializer.Write(original);
            var deserialized = this.serializer.Read(text);

            Assert.That(deserialized.Headers["Content-Type"], Is.EqualTo("text/plain"));
        }
    }
}
