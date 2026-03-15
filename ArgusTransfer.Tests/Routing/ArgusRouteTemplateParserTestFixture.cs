// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRouteTemplateParserTestFixture.cs">
//
//     Copyright (c) 2026 Sam Gerené
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
    using ArgusTransfer.Routing;

    using NUnit.Framework;
    
    /// <summary>
    /// Suite of tests for the <see cref="ArgusRouteTemplateParser"/> class
    /// </summary>
    [TestFixture]
    public class ArgusRouteTemplateParserTestFixture
    {
        [Test]
        public void Verify_that_literal_route_matches()
        {
            var result = ArgusRouteTemplateParser.TryMatch(
                "/healthendpoint",
                "/healthendpoint",
                out var routeValues);

            Assert.That(result, Is.True);
            Assert.That(routeValues, Is.Empty);
        }

        [Test]
        public void Verify_that_literal_route_matches_case_insensitively()
        {
            var result = ArgusRouteTemplateParser.TryMatch(
                "/healthendpoint",
                "/HEALTHENDPOINT",
                out var routeValues);

            Assert.That(result, Is.True);
            Assert.That(routeValues, Is.Empty);
        }

        [Test]
        public void Verify_that_parameter_is_extracted()
        {
            var result = ArgusRouteTemplateParser.TryMatch(
                "/healthendpoint/{name}",
                "/healthendpoint/my-endpoint",
                out var routeValues);

            Assert.That(result, Is.True);
            Assert.That(routeValues["name"], Is.EqualTo("my-endpoint"));
        }

        [Test]
        public void Verify_that_Guid_constraint_validates_and_extracts()
        {
            var guid = "cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e";

            var result = ArgusRouteTemplateParser.TryMatch(
                "/healthendpoint/{identifier:Guid}",
                $"/healthendpoint/{guid}",
                out var routeValues);

            Assert.That(result, Is.True);
            Assert.That(routeValues["identifier"], Is.EqualTo(guid));
        }

        [Test]
        public void Verify_that_Guid_constraint_rejects_invalid_value()
        {
            var result = ArgusRouteTemplateParser.TryMatch(
                "/healthendpoint/{identifier:Guid}",
                "/healthendpoint/not-a-guid",
                out _);

            Assert.That(result, Is.False);
        }

        [Test]
        public void Verify_that_segment_count_mismatch_returns_false()
        {
            var result = ArgusRouteTemplateParser.TryMatch(
                "/healthendpoint",
                "/healthendpoint/extra",
                out _);

            Assert.That(result, Is.False);
        }

        [Test]
        public void Verify_that_literal_mismatch_returns_false()
        {
            var result = ArgusRouteTemplateParser.TryMatch(
                "/healthendpoint",
                "/other",
                out _);

            Assert.That(result, Is.False);
        }

        [Test]
        public void Verify_that_multiple_parameters_are_extracted()
        {
            var result = ArgusRouteTemplateParser.TryMatch(
                "/resource/{type}/{id}",
                "/resource/widget/42",
                out var routeValues);

            Assert.That(result, Is.True);
            Assert.That(routeValues["type"], Is.EqualTo("widget"));
            Assert.That(routeValues["id"], Is.EqualTo("42"));
        }

        [Test]
        public void Verify_that_mixed_literal_and_parameter_segments_match()
        {
            var guid = "cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e";

            var result = ArgusRouteTemplateParser.TryMatch(
                "/api/v1/{resource}/items/{id:Guid}",
                $"/api/v1/orders/items/{guid}",
                out var routeValues);

            Assert.That(result, Is.True);
            Assert.That(routeValues["resource"], Is.EqualTo("orders"));
            Assert.That(routeValues["id"], Is.EqualTo(guid));
        }

        [Test]
        public void Verify_that_root_route_matches()
        {
            var result = ArgusRouteTemplateParser.TryMatch(
                "/",
                "/",
                out var routeValues);

            Assert.That(result, Is.True);
            Assert.That(routeValues, Is.Empty);
        }

        [Test]
        public void Verify_that_Guid_constraint_is_case_insensitive()
        {
            var guid = "cfb2e590-b98a-4dbc-8e56-f5d389ac3a8e";

            var result = ArgusRouteTemplateParser.TryMatch(
                "/resource/{id:guid}",
                $"/resource/{guid}",
                out var routeValues);

            Assert.That(result, Is.True);
            Assert.That(routeValues["id"], Is.EqualTo(guid));
        }

        [Test]
        public void Verify_that_route_with_trailing_slash_does_not_match_without()
        {
            var result = ArgusRouteTemplateParser.TryMatch(
                "/healthendpoint/",
                "/healthendpoint",
                out _);

            Assert.That(result, Is.False);
        }
    }
}
