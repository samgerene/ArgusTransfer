// ------------------------------------------------------------------------------------------------
// <copyright file="PlainTextArgusBodySerializerTestFixture.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

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
