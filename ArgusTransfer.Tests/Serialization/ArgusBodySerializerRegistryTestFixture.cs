// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusBodySerializerRegistryTestFixture.cs">
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

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type

namespace ArgusTransfer.Tests.Serialization
{
    using System;
    using System.Collections.Generic;

    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusBodySerializerRegistry"/> class
    /// </summary>
    [TestFixture]
    public class ArgusBodySerializerRegistryTestFixture
    {
        private ArgusBodySerializerRegistry registry;

        [SetUp]
        public void SetUp()
        {
            var serializers = new List<IArgusBodySerializer>
            {
                new PlainTextArgusBodySerializer()
            };

            this.registry = new ArgusBodySerializerRegistry(serializers);
        }

        [Test]
        public void Verify_that_TryGetSerializer_returns_true_for_registered_content_type()
        {
            var result = this.registry.TryGetSerializer("text/plain", out IArgusBodySerializer serializer);

            Assert.That(result, Is.True);
            Assert.That(serializer, Is.Not.Null);
            Assert.That(serializer.ContentType, Is.EqualTo("text/plain"));
        }

        [Test]
        public void Verify_that_TryGetSerializer_returns_false_for_unregistered_content_type()
        {
            var result = this.registry.TryGetSerializer("application/xml", out _);

            Assert.That(result, Is.False);
        }

        [Test]
        public void Verify_that_DefaultSerializer_returns_plain_text_serializer()
        {
            Assert.That(this.registry.DefaultSerializer, Is.Not.Null);
            Assert.That(this.registry.DefaultSerializer.ContentType, Is.EqualTo("text/plain"));
        }

        [Test]
        public void Verify_that_constructor_throws_when_serializers_empty()
        {
            var emptySerializers = new List<IArgusBodySerializer>();

            Assert.That(() => new ArgusBodySerializerRegistry(emptySerializers), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Verify_that_content_type_lookup_is_case_insensitive()
        {
            var result = this.registry.TryGetSerializer("Text/Plain", out IArgusBodySerializer serializer);

            Assert.That(result, Is.True);
            Assert.That(serializer, Is.Not.Null);
            Assert.That(serializer.ContentType, Is.EqualTo("text/plain"));
        }
    }
}
