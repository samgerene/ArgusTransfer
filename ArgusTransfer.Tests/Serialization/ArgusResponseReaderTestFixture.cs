// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusResponseReaderTestFixture.cs">
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
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusResponseReader"/> class
    /// </summary>
    [TestFixture]
    public class ArgusResponseReaderTestFixture
    {
        [Test]
        public void Verify_that_Read_throws_FormatException_for_empty_input()
        {
            Assert.That(() => ArgusResponseReader.Read(string.Empty),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Read_throws_FormatException_for_status_line_without_first_space()
        {
            Assert.That(() => ArgusResponseReader.Read("ARGUS/1.0"),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Read_throws_FormatException_for_status_line_without_second_space()
        {
            Assert.That(() => ArgusResponseReader.Read("ARGUS/1.0 200"),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Read_throws_FormatException_for_non_integer_status_code()
        {
            Assert.That(() => ArgusResponseReader.Read("ARGUS/1.0 ABC OK"),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Read_throws_FormatException_for_unknown_status_code()
        {
            Assert.That(() => ArgusResponseReader.Read("ARGUS/1.0 999 Unknown"),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Verify_that_Read_ignores_header_line_without_colon()
        {
            var text = "ARGUS/1.0 200 OK\r\nInvalidHeaderWithoutColon\r\n\r\n";

            var response = ArgusResponseReader.Read(text);

            Assert.That(response.Headers, Is.Empty);
        }

        [Test]
        public async Task Verify_that_ReadAsync_throws_FormatException_for_empty_stream()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Empty));
            using var reader = new StreamReader(stream);

            Assert.That(async () => await ArgusResponseReader.ReadAsync(reader, CancellationToken.None),
                Throws.TypeOf<FormatException>());
        }
    }
}
