// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRequestReaderTestFixture.cs" >
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

    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusRequestSerializer"/> class (read path)
    /// </summary>
    [TestFixture]
    public class ArgusRequestReaderTestFixture
    {
        private ArgusRequestSerializer serializer;

        [SetUp]
        public void SetUp()
        {
            this.serializer = new ArgusRequestSerializer();
        }

        [Test]
        public void Verify_that_Read_throws_FormatException_for_empty_input()
        {
            Assert.That(() => this.serializer.Read(string.Empty),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Read_throws_FormatException_for_whitespace_only_input()
        {
            Assert.That(() => this.serializer.Read("   "),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Read_throws_FormatException_for_malformed_request_line()
        {
            Assert.That(() => this.serializer.Read("INVALID"),
                Throws.TypeOf<FormatException>());
        }

        [TestCase("HTTP/1.1")]
        [TestCase("ARGUS/2.0")]
        [TestCase("ARGUS/1.1")]
        [TestCase("argus/1.0")]
        [TestCase("ARGUS/1.0x")]
        [TestCase("")]
        public void Verify_that_Read_throws_FormatException_for_unsupported_protocol_version(string version)
        {
            Assert.That(() => this.serializer.Read($"GET /route {version}\r\n\r\n"),
                Throws.TypeOf<FormatException>().With.Message.Contains("Unsupported protocol version"));
        }

        [Test]
        public void Verify_that_Stream_overload_throws_FormatException_for_unsupported_protocol_version()
        {
            using var stream = new System.IO.MemoryStream(System.Text.Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n"));

            Assert.That(async () => await this.serializer.ReadAsync(stream, System.Threading.CancellationToken.None),
                Throws.TypeOf<FormatException>().With.Message.EqualTo("Unsupported protocol version: 'HTTP/1.1'; expected 'ARGUS/1.0'."));
        }

        [Test]
        public void Verify_that_Read_throws_FormatException_for_unknown_verb()
        {
            Assert.That(() => this.serializer.Read("CONNECT /route ARGUS/1.0"),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Read_ignores_header_line_without_colon()
        {
            var text = "GET /route ARGUS/1.0\r\nInvalidHeaderWithoutColon\r\n\r\n";

            var request = this.serializer.Read(text);

            Assert.That(request.Route, Is.EqualTo("/route"));
            Assert.That(request.Headers, Is.Empty);
        }

        [Test]
        public void Verify_that_Read_preserves_Content_Type_header()
        {
            var text = "GET /route ARGUS/1.0\r\nContent-Type: application/json\r\nX-Custom: value\r\n\r\n";

            var request = this.serializer.Read(text);

            Assert.That(request.Headers["Content-Type"], Is.EqualTo("application/json"));
            Assert.That(request.Headers["X-Custom"], Is.EqualTo("value"));
        }

        [Test]
        public async Task Verify_that_ReadAsync_throws_FormatException_for_empty_stream()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Empty));
            using var reader = new StreamReader(stream);

            await Assert.ThatAsync(async () => await this.serializer.ReadAsync(reader, CancellationToken.None),
                Throws.TypeOf<FormatException>());
        }
    }
}
