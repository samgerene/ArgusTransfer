// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRequestTestFixture.cs">
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

namespace ArgusTransfer.Tests.Protocol
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
    /// Suite of tests for the <see cref="ArgusRequest"/> class
    /// </summary>
    [TestFixture]
    public class ArgusRequestTestFixture
    {
        [Test]
        public void Verify_that_authorization_properties_are_null_without_header()
        {
            var request = new ArgusRequest();

            Assert.That(request.Authorization, Is.Null);
            Assert.That(request.AuthorizationScheme, Is.Null);
            Assert.That(request.AuthorizationParameter, Is.Null);
        }

        [Test]
        public void Verify_that_SetAuthorization_writes_scheme_and_parameter()
        {
            var request = new ArgusRequest();

            request.SetAuthorization("Bearer", "token123");

            Assert.That(request.Headers[ArgusHeaderNames.Authorization], Is.EqualTo("Bearer token123"));
            Assert.That(request.AuthorizationScheme, Is.EqualTo("Bearer"));
            Assert.That(request.AuthorizationParameter, Is.EqualTo("token123"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Verify_that_SetAuthorization_without_parameter_writes_scheme_only(string parameter)
        {
            var request = new ArgusRequest();

            request.SetAuthorization("ApiKey", parameter);

            Assert.That(request.Authorization, Is.EqualTo("ApiKey"));
            Assert.That(request.AuthorizationScheme, Is.EqualTo("ApiKey"));
            Assert.That(request.AuthorizationParameter, Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("Bear er")]
        public void Verify_that_SetAuthorization_rejects_invalid_scheme(string scheme)
        {
            var request = new ArgusRequest();

            Assert.That(() => request.SetAuthorization(scheme, "token"), Throws.InstanceOf<ArgumentException>());
        }

        [TestCase("token\r\nX-Injected: yes")]
        [TestCase("token\nX-Injected: yes")]
        [TestCase("token\r")]
        public void Verify_that_line_breaks_are_rejected_to_prevent_header_injection(string value)
        {
            var request = new ArgusRequest();

            Assert.That(() => request.SetAuthorization("Bearer", value), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => request.Authorization = "Bearer " + value, Throws.InstanceOf<ArgumentException>());
            Assert.That(request.Authorization, Is.Null);
        }

        [TestCase("Bearer token123", "Bearer", "token123")]
        [TestCase("  Bearer    token123  ", "Bearer", "token123")]
        [TestCase("Bearer\ttoken123", "Bearer", "token123")]
        [TestCase("Digest username=\"alice\", realm=\"argus\"", "Digest", "username=\"alice\", realm=\"argus\"")]
        [TestCase("Negotiate", "Negotiate", null)]
        public void Verify_that_raw_Authorization_value_is_parsed(string value, string expectedScheme, string expectedParameter)
        {
            var request = new ArgusRequest { Authorization = value };

            Assert.That(request.AuthorizationScheme, Is.EqualTo(expectedScheme));
            Assert.That(request.AuthorizationParameter, Is.EqualTo(expectedParameter));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Verify_that_clearing_Authorization_removes_the_header(string value)
        {
            var request = new ArgusRequest();
            request.SetAuthorization("Bearer", "token123");

            request.Authorization = value;

            Assert.That(request.Headers.ContainsKey(ArgusHeaderNames.Authorization), Is.False);
            Assert.That(request.AuthorizationScheme, Is.Null);
        }

        [Test]
        public void Verify_that_header_name_is_case_insensitive()
        {
            var request = new ArgusRequest();
            request.Headers["authorization"] = "Bearer token123";

            Assert.That(request.AuthorizationScheme, Is.EqualTo("Bearer"));
            Assert.That(request.AuthorizationParameter, Is.EqualTo("token123"));
        }

        [Test]
        public async Task Verify_that_Authorization_round_trips_through_the_serializer()
        {
            var serializer = new ArgusRequestSerializer();
            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/items" };
            request.SetAuthorization("Bearer", "token123");

            var text = serializer.Write(request);
            Assert.That(text, Does.Contain("Authorization: Bearer token123\r\n"));

            var fromText = serializer.Read(text);

            using var stream = new MemoryStream();
            await serializer.WriteAsync(stream, request);
            stream.Position = 0;
            var fromStream = await serializer.ReadAsync(stream, CancellationToken.None);

            foreach (var result in new[] { fromText, fromStream })
            {
                Assert.That(result.AuthorizationScheme, Is.EqualTo("Bearer"));
                Assert.That(result.AuthorizationParameter, Is.EqualTo("token123"));
            }
        }

        [Test]
        public void Verify_that_parameter_with_colon_and_spaces_survives_header_parsing()
        {
            var serializer = new ArgusRequestSerializer();
            var wire = "GET /items ARGUS/1.0\r\nAuthorization: Basic dXNlcjpwYXNz: extra part\r\n\r\n";

            var result = serializer.Read(wire);

            Assert.That(result.AuthorizationScheme, Is.EqualTo("Basic"));
            Assert.That(result.AuthorizationParameter, Is.EqualTo("dXNlcjpwYXNz: extra part"));
            Assert.That(Encoding.ASCII.GetString(Convert.FromBase64String("dXNlcjpwYXNz")), Is.EqualTo("user:pass"));
        }
    }
}
