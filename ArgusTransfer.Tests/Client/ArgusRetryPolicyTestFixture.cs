// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRetryPolicyTestFixture.cs">
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

namespace ArgusTransfer.Tests.Client
{
    using System;
    using System.IO;

    using ArgusTransfer.Client;
    using ArgusTransfer.Protocol;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusRetryPolicy"/> class
    /// </summary>
    [TestFixture]
    public class ArgusRetryPolicyTestFixture
    {
        [Test]
        public void Verify_that_defaults_are_set()
        {
            var policy = new ArgusRetryPolicy();

            Assert.That(policy.MaxRetries, Is.EqualTo(3));
            Assert.That(policy.InitialDelay, Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(policy.MaxDelay, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(policy.Strategy, Is.EqualTo(RetryBackoffStrategy.Exponential));
            Assert.That(policy.RetryNonIdempotentRequests, Is.False);
            Assert.That(policy.ShouldRetry, Is.Not.Null);
        }

        [TestCase(1, 100)]
        [TestCase(2, 100)]
        [TestCase(5, 100)]
        public void Verify_that_GetDelay_with_Fixed_strategy_returns_initial_delay(int retryAttempt, int expectedMs)
        {
            var policy = new ArgusRetryPolicy { Strategy = RetryBackoffStrategy.Fixed };

            Assert.That(policy.GetDelay(retryAttempt), Is.EqualTo(TimeSpan.FromMilliseconds(expectedMs)));
        }

        [TestCase(1, 100)]
        [TestCase(2, 200)]
        [TestCase(5, 500)]
        public void Verify_that_GetDelay_with_Linear_strategy_grows_linearly(int retryAttempt, int expectedMs)
        {
            var policy = new ArgusRetryPolicy { Strategy = RetryBackoffStrategy.Linear };

            Assert.That(policy.GetDelay(retryAttempt), Is.EqualTo(TimeSpan.FromMilliseconds(expectedMs)));
        }

        [TestCase(1, 100)]
        [TestCase(2, 200)]
        [TestCase(3, 400)]
        [TestCase(5, 1600)]
        public void Verify_that_GetDelay_with_Exponential_strategy_doubles(int retryAttempt, int expectedMs)
        {
            var policy = new ArgusRetryPolicy { Strategy = RetryBackoffStrategy.Exponential };

            Assert.That(policy.GetDelay(retryAttempt), Is.EqualTo(TimeSpan.FromMilliseconds(expectedMs)));
        }

        [Test]
        public void Verify_that_GetDelay_is_capped_at_MaxDelay()
        {
            var policy = new ArgusRetryPolicy { MaxDelay = TimeSpan.FromMilliseconds(250) };

            Assert.That(policy.GetDelay(3), Is.EqualTo(TimeSpan.FromMilliseconds(250)));
        }

        [Test]
        public void Verify_that_GetDelay_does_not_overflow_for_large_retry_attempts()
        {
            var policy = new ArgusRetryPolicy();

            Assert.That(policy.GetDelay(10_000), Is.EqualTo(policy.MaxDelay));
        }

        [Test]
        public void Verify_that_GetDelay_throws_for_retry_attempt_below_one()
        {
            var policy = new ArgusRetryPolicy();

            Assert.That(() => policy.GetDelay(0), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Verify_that_invalid_values_are_rejected()
        {
            var policy = new ArgusRetryPolicy();

            Assert.That(() => policy.MaxRetries = -1, Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => policy.InitialDelay = TimeSpan.FromMilliseconds(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => policy.MaxDelay = TimeSpan.FromMilliseconds(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => policy.Strategy = (RetryBackoffStrategy)42, Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Verify_that_IsTransient_accepts_IOException_and_subclasses_only()
        {
            Assert.That(ArgusRetryPolicy.IsTransient(new IOException()), Is.True);
            Assert.That(ArgusRetryPolicy.IsTransient(new EndOfStreamException()), Is.True);
            Assert.That(ArgusRetryPolicy.IsTransient(new FormatException()), Is.False);
            Assert.That(ArgusRetryPolicy.IsTransient(new UnauthorizedAccessException()), Is.False);
            Assert.That(ArgusRetryPolicy.IsTransient(new TimeoutException()), Is.False);
        }

        [TestCase(ArgusVerb.GET, true)]
        [TestCase(ArgusVerb.HEAD, true)]
        [TestCase(ArgusVerb.PUT, true)]
        [TestCase(ArgusVerb.DELETE, true)]
        [TestCase(ArgusVerb.POST, false)]
        [TestCase(ArgusVerb.PATCH, false)]
        public void Verify_that_IsIdempotent_classifies_verbs(ArgusVerb verb, bool expected)
        {
            Assert.That(ArgusRetryPolicy.IsIdempotent(verb), Is.EqualTo(expected));
        }
    }
}
