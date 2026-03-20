// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusQueryStringHelperTestFixture.cs">
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
    using System.Collections.Generic;

    using ArgusTransfer.Protocol;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusQueryStringHelper"/> class
    /// </summary>
    [TestFixture]
    public class ArgusQueryStringHelperTestFixture
    {
        [Test]
        public void Verify_that_BuildQueryString_returns_empty_for_empty_dictionary()
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var result = ArgusQueryStringHelper.BuildQueryString(parameters);

            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Verify_that_BuildQueryString_returns_empty_for_null()
        {
            var result = ArgusQueryStringHelper.BuildQueryString(null);

            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Verify_that_BuildQueryString_builds_single_parameter()
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "page", "1" }
            };

            var result = ArgusQueryStringHelper.BuildQueryString(parameters);

            Assert.That(result, Is.EqualTo("?page=1"));
        }

        [Test]
        public void Verify_that_BuildQueryString_builds_multiple_parameters()
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "page", "1" },
                { "size", "10" }
            };

            var result = ArgusQueryStringHelper.BuildQueryString(parameters);

            Assert.That(result, Does.StartWith("?"));
            Assert.That(result, Does.Contain("page=1"));
            Assert.That(result, Does.Contain("size=10"));
            Assert.That(result, Does.Contain("&"));
        }

        [Test]
        public void Verify_that_BuildQueryString_percent_encodes_special_characters()
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "name", "hello world" },
                { "filter", "a&b=c" }
            };

            var result = ArgusQueryStringHelper.BuildQueryString(parameters);

            Assert.That(result, Does.Contain("name=hello%20world"));
            Assert.That(result, Does.Contain("filter=a%26b%3Dc"));
        }

        [Test]
        public void Verify_that_ParseQueryString_parses_single_parameter()
        {
            var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            ArgusQueryStringHelper.ParseQueryString("page=1", target);

            Assert.That(target["page"], Is.EqualTo("1"));
        }

        [Test]
        public void Verify_that_ParseQueryString_parses_multiple_parameters()
        {
            var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            ArgusQueryStringHelper.ParseQueryString("page=1&size=10", target);

            Assert.That(target["page"], Is.EqualTo("1"));
            Assert.That(target["size"], Is.EqualTo("10"));
        }

        [Test]
        public void Verify_that_ParseQueryString_decodes_percent_encoded_values()
        {
            var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            ArgusQueryStringHelper.ParseQueryString("name=hello%20world&filter=a%26b%3Dc", target);

            Assert.That(target["name"], Is.EqualTo("hello world"));
            Assert.That(target["filter"], Is.EqualTo("a&b=c"));
        }

        [Test]
        public void Verify_that_ParseQueryString_last_value_wins_for_duplicate_keys()
        {
            var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            ArgusQueryStringHelper.ParseQueryString("page=1&page=2", target);

            Assert.That(target["page"], Is.EqualTo("2"));
        }

        [Test]
        public void Verify_that_ParseQueryString_is_case_insensitive_for_keys()
        {
            var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            ArgusQueryStringHelper.ParseQueryString("Page=1&PAGE=2", target);

            Assert.That(target.Count, Is.EqualTo(1));
            Assert.That(target["page"], Is.EqualTo("2"));
        }

        [Test]
        public void Verify_that_ParseQueryString_handles_empty_string()
        {
            var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            ArgusQueryStringHelper.ParseQueryString("", target);

            Assert.That(target.Count, Is.EqualTo(0));
        }

        [Test]
        public void Verify_that_ParseQueryString_handles_key_without_value()
        {
            var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            ArgusQueryStringHelper.ParseQueryString("flag", target);

            Assert.That(target["flag"], Is.EqualTo(string.Empty));
        }
    }
}
