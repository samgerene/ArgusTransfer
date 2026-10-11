// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusMessageTestFixture.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests.Protocol
{
    using System;

    using ArgusTransfer.Protocol;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusMessage"/> class
    /// </summary>
    [TestFixture]
    public class ArgusMessageTestFixture
    {
        [Test]
        public void Verify_that_default_CorrelationToken_is_not_empty()
        {
            var request = new ArgusRequest();

            Assert.That(request.CorrelationToken, Is.Not.EqualTo(Guid.Empty));
        }

        [Test]
        public void Verify_that_default_Timestamp_is_recent()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var request = new ArgusRequest();
            var after = DateTime.UtcNow.AddSeconds(1);

            Assert.That(request.Timestamp, Is.GreaterThanOrEqualTo(before));
            Assert.That(request.Timestamp, Is.LessThanOrEqualTo(after));
        }

        [Test]
        public void Verify_that_Headers_is_initialized_and_case_insensitive()
        {
            var request = new ArgusRequest();

            request.Headers["Content-Type"] = "text/plain";

            Assert.That(request.Headers["content-type"], Is.EqualTo("text/plain"));
        }
    }
}
