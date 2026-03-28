// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusMessageStreamTestFixture.cs">
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
    using System.IO;

    using ArgusTransfer.Protocol;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusMessage"/> streaming body properties
    /// </summary>
    [TestFixture]
    public class ArgusMessageStreamTestFixture
    {
        [Test]
        public void Verify_that_setting_Body_clears_BodyStream()
        {
            var request = new ArgusRequest
            {
                BodyStream = new MemoryStream(new byte[] { 1, 2, 3 })
            };

            Assert.That(request.IsStreamed, Is.True);

            request.Body = "hello";

            Assert.That(request.BodyStream, Is.Null);
            Assert.That(request.IsStreamed, Is.False);
            Assert.That(request.Body, Is.EqualTo("hello"));
        }

        [Test]
        public void Verify_that_setting_BodyStream_clears_Body()
        {
            var request = new ArgusRequest
            {
                Body = "hello"
            };

            Assert.That(request.IsStreamed, Is.False);

            var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            request.BodyStream = stream;

            Assert.That(request.Body, Is.Null);
            Assert.That(request.IsStreamed, Is.True);
            Assert.That(request.BodyStream, Is.SameAs(stream));
        }

        [Test]
        public void Verify_that_IsStreamed_returns_true_when_BodyStream_is_set()
        {
            var response = new ArgusResponse
            {
                BodyStream = new MemoryStream()
            };

            Assert.That(response.IsStreamed, Is.True);
        }

        [Test]
        public void Verify_that_IsStreamed_returns_false_when_only_Body_is_set()
        {
            var response = new ArgusResponse
            {
                Body = "text"
            };

            Assert.That(response.IsStreamed, Is.False);
        }

        [Test]
        public void Verify_that_IsStreamed_returns_false_when_neither_is_set()
        {
            var request = new ArgusRequest();

            Assert.That(request.IsStreamed, Is.False);
        }
    }
}
