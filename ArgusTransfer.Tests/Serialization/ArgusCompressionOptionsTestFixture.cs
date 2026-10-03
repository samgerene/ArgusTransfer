// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusCompressionOptionsTestFixture.cs">
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
