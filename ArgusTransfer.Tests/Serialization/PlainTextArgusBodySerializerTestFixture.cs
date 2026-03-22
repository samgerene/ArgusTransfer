// -------------------------------------------------------------------------------------------------
//   <copyright file="PlainTextArgusBodySerializerTestFixture.cs">
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
    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="PlainTextArgusBodySerializer"/> class
    /// </summary>
    [TestFixture]
    public class PlainTextArgusBodySerializerTestFixture
    {
        private PlainTextArgusBodySerializer serializer;

        [SetUp]
        public void SetUp()
        {
            this.serializer = new PlainTextArgusBodySerializer();
        }

        [Test]
        public void Verify_that_ContentType_is_text_plain()
        {
            Assert.That(this.serializer.ContentType, Is.EqualTo("text/plain"));
        }

        [Test]
        public void Verify_that_WriteBody_returns_body_unchanged()
        {
            var body = "Hello, world!";

            var result = this.serializer.WriteBody(body);

            Assert.That(result, Is.EqualTo(body));
        }

        [Test]
        public void Verify_that_ReadBody_returns_body_unchanged()
        {
            var body = "Hello, world!";

            var result = this.serializer.ReadBody(body);

            Assert.That(result, Is.EqualTo(body));
        }
    }
}
