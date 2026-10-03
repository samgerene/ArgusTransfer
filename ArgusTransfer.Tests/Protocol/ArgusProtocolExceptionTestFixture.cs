// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusProtocolExceptionTestFixture.cs">
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
