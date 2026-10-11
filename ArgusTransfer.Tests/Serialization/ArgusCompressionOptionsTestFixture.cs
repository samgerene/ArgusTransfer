// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusCompressionOptionsTestFixture.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests.Serialization
{
    using System;

    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusCompressionOptions"/> class
    /// </summary>
    [TestFixture]
    public class ArgusCompressionOptionsTestFixture
    {
        [Test]
        public void Verify_that_defaults_are_set()
        {
            var options = new ArgusCompressionOptions();

            Assert.That(options.Enabled, Is.False);
            Assert.That(options.MinimumBodySize, Is.EqualTo(1024));
            Assert.That(options.PreferredEncoding, Is.EqualTo("gzip"));
            Assert.That(options.Encodings, Has.Count.EqualTo(1));
            Assert.That(options.Encodings[0], Is.TypeOf<GZipArgusContentEncoding>());
        }

        [Test]
        public void Verify_that_invalid_values_are_rejected()
        {
            var options = new ArgusCompressionOptions();

            Assert.That(() => options.MinimumBodySize = -1, Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => options.PreferredEncoding = null, Throws.InstanceOf<ArgumentException>());
            Assert.That(() => options.PreferredEncoding = " ", Throws.InstanceOf<ArgumentException>());
        }
    }
}
