// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRequestExceptionTestFixture.cs">
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

    using ArgusTransfer.Protocol;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusRequestException"/> class
    /// </summary>
    [TestFixture]
    public class ArgusRequestExceptionTestFixture
    {
        [Test]
        public void Verify_that_default_constructor_does_not_throw()
        {
            Assert.That(() => new ArgusRequestException(), Throws.Nothing);
        }

        [Test]
        public void Verify_that_message_constructor_sets_message()
        {
            var exception = new ArgusRequestException("boom");

            Assert.That(exception.Message, Is.EqualTo("boom"));
        }

        [Test]
        public void Verify_that_message_and_inner_constructor_sets_both()
        {
            var inner = new InvalidOperationException("inner");

            var exception = new ArgusRequestException("boom", inner);

            Assert.That(exception.Message, Is.EqualTo("boom"));
            Assert.That(exception.InnerException, Is.SameAs(inner));
        }

        [Test]
        public void Verify_that_response_constructor_populates_all_properties()
        {
            var exception = new ArgusRequestException(ArgusStatusCode.NotFound, "Not Found", "missing");

            Assert.That(exception.StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));
            Assert.That(exception.ReasonPhrase, Is.EqualTo("Not Found"));
            Assert.That(exception.ResponseBody, Is.EqualTo("missing"));
        }

        [Test]
        public void Verify_that_response_constructor_builds_message_with_status_and_reason()
        {
            var exception = new ArgusRequestException(ArgusStatusCode.InternalServerError, "Internal Server Error", null);

            Assert.That(exception.Message, Does.Contain("500"));
            Assert.That(exception.Message, Does.Contain("Internal Server Error"));
        }

        [Test]
        public void Verify_that_response_constructor_accepts_null_body()
        {
            var exception = new ArgusRequestException(ArgusStatusCode.NoContent, "No Content", null);

            Assert.That(exception.ResponseBody, Is.Null);
        }
    }
}
