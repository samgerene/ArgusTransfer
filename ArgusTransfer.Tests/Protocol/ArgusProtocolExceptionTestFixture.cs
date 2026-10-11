// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusProtocolExceptionTestFixture.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests.Protocol
{
    using System;
    using System.IO;

    using ArgusTransfer.Protocol;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusProtocolException"/> class
    /// </summary>
    [TestFixture]
    public class ArgusProtocolExceptionTestFixture
    {
        [Test]
        public void Verify_that_constructors_set_message_and_inner_exception()
        {
            var inner = new InvalidDataException("bad gzip");

            Assert.That(new ArgusProtocolException().Message, Is.Not.Empty);
            Assert.That(new ArgusProtocolException("too large").Message, Is.EqualTo("too large"));

            var withInner = new ArgusProtocolException("corrupt", inner);

            Assert.That(withInner.Message, Is.EqualTo("corrupt"));
            Assert.That(withInner.InnerException, Is.SameAs(inner));
        }

        [Test]
        public void Verify_that_it_derives_from_InvalidOperationException_for_compatibility()
        {
            Assert.That(new ArgusProtocolException("x"), Is.InstanceOf<InvalidOperationException>());
        }
    }
}
