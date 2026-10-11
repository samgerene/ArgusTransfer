// ------------------------------------------------------------------------------------------------
// <copyright file="ShortGuidRouteConstraintTestFixture.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests.Routing
{
    using System;

    using ArgusTransfer.Extensions;
    using ArgusTransfer.Routing;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ShortGuidRouteConstraint"/> class
    /// </summary>
    [TestFixture]
    public class ShortGuidRouteConstraintTestFixture
    {
        private ShortGuidRouteConstraint constraint;

        [SetUp]
        public void SetUp()
        {
            this.constraint = new ShortGuidRouteConstraint();
        }

        [Test]
        public void Verify_that_Match_returns_true_for_a_valid_ShortGuid()
        {
            var shortGuid = Guid.NewGuid().ToShortGuid();

            var result = this.constraint.Match(shortGuid);

            Assert.That(result, Is.True);
        }

        [Test]
        public void Verify_that_Match_returns_true_for_a_ShortGuid_containing_url_safe_characters()
        {
            var shortGuid = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff").ToShortGuid();

            var result = this.constraint.Match(shortGuid);

            Assert.That(result, Is.True);
            Assert.That(shortGuid, Does.Contain("_").Or.Contain("-"));
        }

        [Test]
        public void Verify_that_Match_returns_false_for_a_segment_shorter_than_22_characters()
        {
            var result = this.constraint.Match("too-short");

            Assert.That(result, Is.False);
        }

        [Test]
        public void Verify_that_Match_returns_false_for_a_segment_longer_than_22_characters()
        {
            var result = this.constraint.Match("this-string-is-far-too-long-to-be-a-short-guid");

            Assert.That(result, Is.False);
        }

        [Test]
        public void Verify_that_Match_returns_false_for_an_empty_segment()
        {
            var result = this.constraint.Match(string.Empty);

            Assert.That(result, Is.False);
        }

        [Test]
        public void Verify_that_Match_returns_false_for_a_22_character_segment_with_invalid_base64_characters()
        {
            var invalidBase64 = new string('!', 22);

            var result = this.constraint.Match(invalidBase64);

            Assert.That(result, Is.False);
        }
    }
}
