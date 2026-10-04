// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRouteTemplateTestFixture.cs">
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

namespace ArgusTransfer.Tests.Routing
{
    using System;

    using ArgusTransfer.Routing;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusRouteTemplate"/> class
    /// </summary>
    [TestFixture]
    public class ArgusRouteTemplateTestFixture
    {
        [Test]
        public void Verify_that_parsed_template_matches_literals_case_insensitively_and_extracts_parameters()
        {
            var id = Guid.NewGuid().ToString();
            var template = ArgusRouteTemplate.Parse("/Items/{id:Guid}/parts/{name}");

            var matched = template.TryMatch($"/items/{id}/PARTS/wheel".Split('/'), out var routeValues);

            Assert.That(matched, Is.True);
            Assert.That(routeValues["ID"], Is.EqualTo(id));
            Assert.That(routeValues["name"], Is.EqualTo("wheel"));
            Assert.That(routeValues, Has.Count.EqualTo(2));
        }

        [Test]
        public void Verify_that_parsed_template_rejects_non_matching_routes_without_route_values()
        {
            var template = ArgusRouteTemplate.Parse("/items/{id:Guid}");

            Assert.That(template.TryMatch("/items/not-a-guid".Split('/'), out var constraintFailed), Is.False);
            Assert.That(constraintFailed, Is.Null);

            Assert.That(template.TryMatch("/orders/3f2504e0-4f89-11d3-9a0c-0305e82c3301".Split('/'), out var literalFailed), Is.False);
            Assert.That(literalFailed, Is.Null);

            Assert.That(template.TryMatch("/items".Split('/'), out var lengthFailed), Is.False);
            Assert.That(lengthFailed, Is.Null);
        }

        [Test]
        public void Verify_that_parse_rejects_invalid_templates()
        {
            Assert.That(() => ArgusRouteTemplate.Parse(null), Throws.ArgumentNullException);
            Assert.That(() => ArgusRouteTemplate.Parse("/items/{id:Giud}"), Throws.ArgumentException.With.Message.Contain("'Giud'"));
            Assert.That(() => ArgusRouteTemplate.Parse("/items/{:Guid}"), Throws.ArgumentException);
        }

        [Test]
        public void Verify_that_parse_keeps_the_template_text()
        {
            Assert.That(ArgusRouteTemplate.Parse("/items/{id}").Text, Is.EqualTo("/items/{id}"));
        }
    }
}
