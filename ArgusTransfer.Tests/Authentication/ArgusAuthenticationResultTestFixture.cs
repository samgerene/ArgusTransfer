// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusAuthenticationResultTestFixture.cs">
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

namespace ArgusTransfer.Tests.Authentication
{
    using System;
    using System.Security.Claims;

    using ArgusTransfer.Authentication;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusAuthenticationResult"/> class
    /// </summary>
    [TestFixture]
    public class ArgusAuthenticationResultTestFixture
    {
        [Test]
        public void Verify_that_Success_carries_the_principal()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"));

            var result = ArgusAuthenticationResult.Success(principal);

            Assert.That(result.Status, Is.EqualTo(ArgusAuthenticationStatus.Success));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Principal, Is.SameAs(principal));
            Assert.That(result.FailureReason, Is.Null);
        }

        [Test]
        public void Verify_that_Success_throws_for_null_principal()
        {
            Assert.That(() => ArgusAuthenticationResult.Success(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Verify_that_non_success_results_carry_status_and_reason()
        {
            var none = ArgusAuthenticationResult.NoResult();
            var failed = ArgusAuthenticationResult.Fail("expired token");
            var forbidden = ArgusAuthenticationResult.Forbidden("read-only client");

            Assert.That(none.Status, Is.EqualTo(ArgusAuthenticationStatus.NoResult));
            Assert.That(failed.Status, Is.EqualTo(ArgusAuthenticationStatus.Failure));
            Assert.That(failed.FailureReason, Is.EqualTo("expired token"));
            Assert.That(forbidden.Status, Is.EqualTo(ArgusAuthenticationStatus.Forbidden));
            Assert.That(forbidden.FailureReason, Is.EqualTo("read-only client"));

            foreach (var result in new[] { none, failed, forbidden })
            {
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Principal, Is.Null);
            }
        }
    }
}
