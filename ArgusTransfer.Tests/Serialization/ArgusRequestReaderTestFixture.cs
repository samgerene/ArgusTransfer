// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRequestReaderTestFixture.cs" company="Sam Gerene">
//
//     Copyright (c) 2026 Sam Gerene
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
    /// Suite of tests for the <see cref="ArgusRequestReader"/> class
    /// </summary>
    [TestFixture]
    public class ArgusRequestReaderTestFixture
    {
        [Test]
        public void Verify_that_Read_throws_FormatException_for_empty_input()
        {
            Assert.That(() => ArgusRequestReader.Read(string.Empty),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Read_throws_FormatException_for_whitespace_only_input()
        {
            Assert.That(() => ArgusRequestReader.Read("   "),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Read_throws_FormatException_for_malformed_request_line()
        {
            Assert.That(() => ArgusRequestReader.Read("INVALID"),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Read_throws_FormatException_for_unknown_verb()
        {
            Assert.That(() => ArgusRequestReader.Read("CONNECT /route ARGUS/1.0"),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Read_ignores_header_line_without_colon()
        {
            var text = "GET /route ARGUS/1.0\r\nInvalidHeaderWithoutColon\r\n\r\n";

            var request = ArgusRequestReader.Read(text);

            Assert.That(request.Route, Is.EqualTo("/route"));
            Assert.That(request.Headers, Is.Empty);
        }

        [Test]
        public void Verify_that_Read_skips_Content_Type_header()
        {
            var text = "GET /route ARGUS/1.0\r\nContent-Type: application/json\r\nX-Custom: value\r\n\r\n";

            var request = ArgusRequestReader.Read(text);

            Assert.That(request.Headers.ContainsKey("Content-Type"), Is.False);
            Assert.That(request.Headers["X-Custom"], Is.EqualTo("value"));
        }

        [Test]
        public async Task Verify_that_ReadAsync_throws_FormatException_for_empty_stream()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Empty));
            using var reader = new StreamReader(stream);

            Assert.That(async () => await ArgusRequestReader.ReadAsync(reader, CancellationToken.None),
                Throws.TypeOf<FormatException>());
        }
    }
}
