// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusContentNegotiationTestFixture.cs">
//
//     Copyright (c) 2025-2026 Sam Gerene
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
    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusContentNegotiation"/> class
    /// </summary>
    [TestFixture]
    public class ArgusContentNegotiationTestFixture
    {
        private PlainTextArgusBodySerializer plainText;

        private TaggingBodySerializer json;

        private TaggingBodySerializer xml;

        private ArgusBodySerializerRegistry registry;

        [SetUp]
        public void SetUp()
        {
            this.plainText = new PlainTextArgusBodySerializer();
            this.json = new TaggingBodySerializer("application/json");
            this.xml = new TaggingBodySerializer("application/xml");
            this.registry = new ArgusBodySerializerRegistry([this.plainText, this.json, this.xml]);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not a media type")]
        [TestCase("application/json;q=abc")]
        public void Verify_that_no_usable_preference_selects_the_default_serializer(string accept)
        {
            Assert.That(ArgusContentNegotiation.SelectSerializer(this.registry, accept), Is.SameAs(this.plainText));
        }

        [TestCase("application/json, text/plain", "application/json")]
        [TestCase("text/plain, application/json", "text/plain")]
        [TestCase("text/plain;q=0.5, application/json", "application/json")]
        [TestCase("application/json;q=0.9", "application/json")]
        [TestCase("Application/JSON", "application/json")]
        [TestCase("application/vnd.unknown, application/xml;q=0.3", "application/xml")]
        [TestCase("application/*", "application/json")]
        [TestCase("application/*, application/xml", "application/xml")]
        [TestCase("*/*", "text/plain")]
        [TestCase("*", "text/plain")]
        [TestCase("*/*, text/plain;q=0", "application/json")]
        [TestCase("text/*;q=0.2, application/json;q=0.1", "text/plain")]
        [TestCase("application/json;q=abc, text/plain;q=0.1", "text/plain")]
        [TestCase("application/json; charset=utf-8; q=1", "application/json")]
        [TestCase("application/*;q=0.5, application/json;q=0, */*;q=0.1", "application/xml")]
        public void Verify_that_the_best_acceptable_serializer_is_selected(string accept, string expectedContentType)
        {
            var selected = ArgusContentNegotiation.SelectSerializer(this.registry, accept);

            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.ContentType, Is.EqualTo(expectedContentType));
        }

        [TestCase("application/vnd.unknown")]
        [TestCase("application/json;q=0")]
        [TestCase("image/*")]
        [TestCase("*/*;q=0")]
        [TestCase("text/plain;q=0, application/*;q=0")]
        public void Verify_that_nothing_is_selected_when_no_serializer_is_acceptable(string accept)
        {
            Assert.That(ArgusContentNegotiation.SelectSerializer(this.registry, accept), Is.Null);
        }

        [Test]
        public void Verify_that_a_registry_without_GetSerializers_still_matches_exact_media_types()
        {
            var minimal = new MinimalRegistry(this.plainText, this.json);

            Assert.That(ArgusContentNegotiation.SelectSerializer(minimal, "application/xml, application/json;q=0.5"), Is.SameAs(this.json));
            Assert.That(ArgusContentNegotiation.SelectSerializer(minimal, "text/*"), Is.SameAs(this.plainText));
            Assert.That(ArgusContentNegotiation.SelectSerializer(minimal, "application/*"), Is.Null);
        }

        [Test]
        public void Verify_that_Parse_reads_media_ranges_with_quality_values()
        {
            var ranges = ArgusContentNegotiation.Parse("text/plain;q=0.5, application/*, bogus, */*;q=0");

            Assert.That(ranges, Has.Count.EqualTo(3));
            Assert.That(ranges[0], Is.EqualTo(new ArgusContentNegotiation.MediaRange("text", "plain", 0.5, 0)));
            Assert.That(ranges[1].Specificity, Is.EqualTo(1));
            Assert.That(ranges[1].Index, Is.EqualTo(1));
            Assert.That(ranges[2].Specificity, Is.EqualTo(0));
            Assert.That(ranges[2].Quality, Is.Zero);
        }

        [Test]
        public void Verify_that_registry_GetSerializers_returns_registered_serializers_in_order()
        {
            Assert.That(this.registry.GetSerializers(), Is.EqualTo(new IArgusBodySerializer[] { this.plainText, this.json, this.xml }));
        }

        /// <summary>
        /// A registry that only implements the original members, relying on the default <c>GetSerializers</c>
        /// </summary>
        private sealed class MinimalRegistry : IArgusBodySerializerRegistry
        {
            private readonly IArgusBodySerializer other;

            public MinimalRegistry(IArgusBodySerializer defaultSerializer, IArgusBodySerializer other)
            {
                this.DefaultSerializer = defaultSerializer;
                this.other = other;
            }

            public IArgusBodySerializer DefaultSerializer { get; }

            public bool TryGetSerializer(string contentType, out IArgusBodySerializer serializer)
            {
                serializer = contentType == this.other.ContentType ? this.other : null;
                return serializer != null;
            }
        }
    }
}
