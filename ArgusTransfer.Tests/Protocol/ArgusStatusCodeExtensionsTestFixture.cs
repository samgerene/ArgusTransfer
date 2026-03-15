// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusStatusCodeExtensionsTestFixture.cs" >
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
    using ArgusTransfer.Protocol;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusStatusCodeExtensions"/> class
    /// </summary>
    [TestFixture]
    public class ArgusStatusCodeExtensionsTestFixture
    {
        [Test]
        public void Verify_that_Ok_returns_OK_reason_phrase()
        {
            Assert.That(ArgusStatusCode.Ok.ToReasonPhrase(), Is.EqualTo("OK"));
        }

        [Test]
        public void Verify_that_Created_returns_Created_reason_phrase()
        {
            Assert.That(ArgusStatusCode.Created.ToReasonPhrase(), Is.EqualTo("Created"));
        }

        [Test]
        public void Verify_that_BadRequest_returns_Bad_Request_reason_phrase()
        {
            Assert.That(ArgusStatusCode.BadRequest.ToReasonPhrase(), Is.EqualTo("Bad Request"));
        }

        [Test]
        public void Verify_that_NotFound_returns_Not_Found_reason_phrase()
        {
            Assert.That(ArgusStatusCode.NotFound.ToReasonPhrase(), Is.EqualTo("Not Found"));
        }

        [Test]
        public void Verify_that_InternalServerError_returns_Internal_Server_Error_reason_phrase()
        {
            Assert.That(ArgusStatusCode.InternalServerError.ToReasonPhrase(), Is.EqualTo("Internal Server Error"));
        }

        [Test]
        public void Verify_that_TryParse_succeeds_for_valid_status_codes()
        {
            Assert.That(ArgusStatusCodeExtensions.TryParse(200, out var ok), Is.True);
            Assert.That(ok, Is.EqualTo(ArgusStatusCode.Ok));

            Assert.That(ArgusStatusCodeExtensions.TryParse(201, out var created), Is.True);
            Assert.That(created, Is.EqualTo(ArgusStatusCode.Created));

            Assert.That(ArgusStatusCodeExtensions.TryParse(400, out var badRequest), Is.True);
            Assert.That(badRequest, Is.EqualTo(ArgusStatusCode.BadRequest));

            Assert.That(ArgusStatusCodeExtensions.TryParse(404, out var notFound), Is.True);
            Assert.That(notFound, Is.EqualTo(ArgusStatusCode.NotFound));

            Assert.That(ArgusStatusCodeExtensions.TryParse(500, out var internalServerError), Is.True);
            Assert.That(internalServerError, Is.EqualTo(ArgusStatusCode.InternalServerError));
        }

        [Test]
        public void Verify_that_TryParse_fails_for_invalid_status_code()
        {
            Assert.That(ArgusStatusCodeExtensions.TryParse(999, out _), Is.False);
        }
    }
}
