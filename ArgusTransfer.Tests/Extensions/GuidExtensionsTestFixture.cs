// -------------------------------------------------------------------------------------------------
// <copyright file="GuidExtensionsTestFixture.cs" >
//   Copyright (C) 2026 Sam Gerené
// 
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
//   Unless required by applicable law or agreed to in writing, softwareUseCases
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
// 
// </copyright>
//  ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ArgusTransfer.Extensions;
    
    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="GuidExtensions"/> class
    /// </summary>
    [TestFixture]
    public class GuidExtensionsTestFixture
    {
        [Test]
        public void Verify_that_ToShortGuid_from_string_returns_valid_short_guid()
        {
            var guidString = "cfb2e590-eed6-4223-b2ba-271ed0cb06da";

            var shortGuid = guidString.ToShortGuid();

            Assert.That(shortGuid, Is.Not.Null.And.Not.Empty);
            Assert.That(shortGuid.Length, Is.EqualTo(22));
        }

        [Test]
        public void Verify_that_ToShortGuid_from_Guid_returns_valid_short_guid()
        {
            var guid = Guid.Parse("cfb2e590-eed6-4223-b2ba-271ed0cb06da");

            var shortGuid = guid.ToShortGuid();

            Assert.That(shortGuid, Is.Not.Null.And.Not.Empty);
            Assert.That(shortGuid.Length, Is.EqualTo(22));
        }

        [Test]
        public void Verify_that_FromShortGuid_returns_original_Guid()
        {
            var originalGuid = Guid.Parse("cfb2e590-eed6-4223-b2ba-271ed0cb06da");

            var shortGuid = originalGuid.ToShortGuid();
            var roundTripped = shortGuid.FromShortGuid();

            Assert.That(roundTripped, Is.EqualTo(originalGuid));
        }

        [Test]
        public void Verify_that_ToShortGuids_from_strings_returns_valid_short_guids()
        {
            var guidStrings = new List<string>
            {
                "cfb2e590-eed6-4223-b2ba-271ed0cb06da",
                "fe08550c-d926-4758-808f-a9a991e0ff37"
            };

            var shortGuids = guidStrings.ToShortGuids().ToList();

            Assert.That(shortGuids.Count, Is.EqualTo(2));
            Assert.That(shortGuids.All(sg => sg.Length == 22), Is.True);
        }

        [Test]
        public void Verify_that_ToShortGuids_from_Guids_returns_valid_short_guids()
        {
            var guids = new List<Guid>
            {
                Guid.Parse("cfb2e590-eed6-4223-b2ba-271ed0cb06da"),
                Guid.Parse("fe08550c-d926-4758-808f-a9a991e0ff37")
            };

            var shortGuids = guids.ToShortGuids().ToList();

            Assert.That(shortGuids.Count, Is.EqualTo(2));
            Assert.That(shortGuids.All(sg => sg.Length == 22), Is.True);
        }

        [Test]
        public void Verify_that_ToShortGuidArray_returns_bracket_delimited_string()
        {
            var guids = new List<Guid>
            {
                Guid.Parse("cfb2e590-eed6-4223-b2ba-271ed0cb06da"),
                Guid.Parse("fe08550c-d926-4758-808f-a9a991e0ff37")
            };

            var array = guids.ToShortGuidArray();

            Assert.That(array, Does.StartWith("["));
            Assert.That(array, Does.EndWith("]"));
            Assert.That(array, Does.Contain(";"));
        }

        [Test]
        public void Verify_that_FromShortGuidArray_returns_original_Guids()
        {
            var guids = new List<Guid>
            {
                Guid.Parse("cfb2e590-eed6-4223-b2ba-271ed0cb06da"),
                Guid.Parse("fe08550c-d926-4758-808f-a9a991e0ff37")
            };

            var array = guids.ToShortGuidArray();
            var roundTripped = array.FromShortGuidArray().ToList();

            Assert.That(roundTripped.Count, Is.EqualTo(2));
            Assert.That(roundTripped[0], Is.EqualTo(guids[0]));
            Assert.That(roundTripped[1], Is.EqualTo(guids[1]));
        }

        [Test]
        public void Verify_that_FromShortGuidArray_throws_when_missing_opening_bracket()
        {
            var invalidArray = "abc]";

            Assert.That(() => invalidArray.FromShortGuidArray().ToList(),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Verify_that_FromShortGuidArray_throws_when_missing_closing_bracket()
        {
            var invalidArray = "[abc";

            Assert.That(() => invalidArray.FromShortGuidArray().ToList(),
                Throws.TypeOf<ArgumentException>());
        }
    }
}
