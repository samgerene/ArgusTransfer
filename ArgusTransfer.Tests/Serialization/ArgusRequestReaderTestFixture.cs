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

            Assert.That(async () => await this.serializer.ReadAsync(reader, CancellationToken.None),
                Throws.TypeOf<FormatException>());
        }
    }
}
