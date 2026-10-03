// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusProblemDetailsTestFixture.cs">
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
    using System.Text.Json;
    using System.Threading;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;
    using ArgusTransfer.Serialization;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusProblemDetails"/> class
    /// </summary>
    [TestFixture]
    public class ArgusProblemDetailsTestFixture
    {
        [Test]
        public void Verify_that_ToJson_writes_standard_members_in_lower_case()
        {
            var problem = new ArgusProblemDetails
            {
                Type = "urn:argus:validation",
                Title = "Validation failed",
                Status = 400,
                Detail = "Name is required",
                Instance = "abc"
            };

            using var document = JsonDocument.Parse(problem.ToJson());
            var root = document.RootElement;

            Assert.That(root.GetProperty("type").GetString(), Is.EqualTo("urn:argus:validation"));
            Assert.That(root.GetProperty("title").GetString(), Is.EqualTo("Validation failed"));
            Assert.That(root.GetProperty("status").GetInt32(), Is.EqualTo(400));
            Assert.That(root.GetProperty("detail").GetString(), Is.EqualTo("Name is required"));
            Assert.That(root.GetProperty("instance").GetString(), Is.EqualTo("abc"));
        }

        [Test]
        public void Verify_that_ToJson_omits_null_members()
        {
            var problem = new ArgusProblemDetails { Status = 404 };

            using var document = JsonDocument.Parse(problem.ToJson());
            var root = document.RootElement;

            Assert.That(root.TryGetProperty("type", out _), Is.False);
            Assert.That(root.TryGetProperty("title", out _), Is.False);
            Assert.That(root.TryGetProperty("detail", out _), Is.False);
            Assert.That(root.TryGetProperty("instance", out _), Is.False);
            Assert.That(root.GetProperty("status").GetInt32(), Is.EqualTo(404));
        }

        [Test]
        public void Verify_that_ToJson_writes_extensions_as_top_level_members()
        {
            var problem = new ArgusProblemDetails { Status = 400 };
            problem.Extensions["field"] = "name";
            problem.Extensions["attempts"] = 3;

            using var document = JsonDocument.Parse(problem.ToJson());
            var root = document.RootElement;

            Assert.That(root.GetProperty("field").GetString(), Is.EqualTo("name"));
            Assert.That(root.GetProperty("attempts").GetInt32(), Is.EqualTo(3));
            Assert.That(root.TryGetProperty("Extensions", out _), Is.False);
        }

        [Test]
        public void Verify_that_problem_details_survive_a_json_round_trip()
        {
            var original = new ArgusProblemDetails
            {
                Type = "urn:argus:conflict",
                Title = "Conflict",
                Status = 409,
                Detail = "Item already exists",
                Instance = Guid.NewGuid().ToString()
            };
            original.Extensions["itemId"] = "42";
            original.Extensions["retryable"] = false;

            var result = ArgusProblemDetails.FromJson(original.ToJson());

            Assert.That(result.Type, Is.EqualTo(original.Type));
            Assert.That(result.Title, Is.EqualTo(original.Title));
            Assert.That(result.Status, Is.EqualTo(original.Status));
            Assert.That(result.Detail, Is.EqualTo(original.Detail));
            Assert.That(result.Instance, Is.EqualTo(original.Instance));
            Assert.That(result.Extensions, Has.Count.EqualTo(2));
            Assert.That(((JsonElement)result.Extensions["itemId"]).GetString(), Is.EqualTo("42"));
            Assert.That(((JsonElement)result.Extensions["retryable"]).GetBoolean(), Is.False);
        }

        [Test]
        public void Verify_that_FromJson_throws_for_null()
        {
            Assert.That(() => ArgusProblemDetails.FromJson(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Verify_that_FromJson_throws_for_invalid_json()
        {
            Assert.That(() => ArgusProblemDetails.FromJson("not json"), Throws.InstanceOf<JsonException>());
        }

        [Test]
        public void Verify_that_FromJson_throws_for_json_null_literal()
        {
            Assert.That(() => ArgusProblemDetails.FromJson("null"), Throws.InstanceOf<JsonException>());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not json")]
        [TestCase("null")]
        [TestCase("[1, 2]")]
        public void Verify_that_TryParse_returns_false_for_invalid_input(string json)
        {
            var result = ArgusProblemDetails.TryParse(json, out var problem);

            Assert.That(result, Is.False);
            Assert.That(problem, Is.Null);
        }

        [Test]
        public void Verify_that_TryParse_returns_true_for_valid_json()
        {
            var result = ArgusProblemDetails.TryParse("{\"title\":\"Not Found\",\"status\":404}", out var problem);

            Assert.That(result, Is.True);
            Assert.That(problem.Title, Is.EqualTo("Not Found"));
            Assert.That(problem.Status, Is.EqualTo(404));
        }

        [Test]
        public void Verify_that_Create_applies_defaults_from_status_code()
        {
            var problem = ArgusProblemDetails.Create(ArgusStatusCode.NotFound, "Item 42 not found");

            Assert.That(problem.Type, Is.EqualTo(ArgusProblemDetails.DefaultType));
            Assert.That(problem.Title, Is.EqualTo(ArgusStatusCode.NotFound.ToReasonPhrase()));
            Assert.That(problem.Status, Is.EqualTo(404));
            Assert.That(problem.Detail, Is.EqualTo("Item 42 not found"));
            Assert.That(problem.Instance, Is.Null);
            Assert.That(problem.Extensions, Is.Empty);
        }

        [Test]
        public void Verify_that_Create_uses_explicit_title_type_instance_and_extensions()
        {
            var extensions = new Dictionary<string, object> { ["field"] = "name" };

            var problem = ArgusProblemDetails.Create(
                ArgusStatusCode.UnprocessableEntity,
                "Name is too long",
                extensions,
                "Validation failed",
                "urn:argus:validation",
                "instance-1");

            Assert.That(problem.Type, Is.EqualTo("urn:argus:validation"));
            Assert.That(problem.Title, Is.EqualTo("Validation failed"));
            Assert.That(problem.Status, Is.EqualTo(422));
            Assert.That(problem.Instance, Is.EqualTo("instance-1"));
            Assert.That(problem.Extensions["field"], Is.EqualTo("name"));
            Assert.That(problem.Extensions, Is.Not.SameAs(extensions));
        }

        [Test]
        public void Verify_that_ToResponse_sets_status_content_type_and_body()
        {
            var problem = ArgusProblemDetails.Create(ArgusStatusCode.Conflict, "Item already exists");

            var response = problem.ToResponse();

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Conflict));
            Assert.That(response.Headers[ArgusHeaderNames.ContentType], Is.EqualTo(ArgusProblemDetails.ContentType));
            Assert.That(ArgusProblemDetails.FromJson(response.Body).Detail, Is.EqualTo("Item already exists"));
        }

        [Test]
        public void Verify_that_ToResponse_throws_for_undefined_status()
        {
            var problem = new ArgusProblemDetails { Status = 418 };

            Assert.That(() => problem.ToResponse(), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Verify_that_factory_methods_produce_matching_status_codes()
        {
            Assert.That(ArgusProblemDetails.BadRequest().StatusCode, Is.EqualTo(ArgusStatusCode.BadRequest));
            Assert.That(ArgusProblemDetails.Unauthorized().StatusCode, Is.EqualTo(ArgusStatusCode.Unauthorized));
            Assert.That(ArgusProblemDetails.Forbidden().StatusCode, Is.EqualTo(ArgusStatusCode.Forbidden));
            Assert.That(ArgusProblemDetails.NotFound().StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));
            Assert.That(ArgusProblemDetails.Conflict().StatusCode, Is.EqualTo(ArgusStatusCode.Conflict));
            Assert.That(ArgusProblemDetails.UnprocessableEntity().StatusCode, Is.EqualTo(ArgusStatusCode.UnprocessableEntity));
            Assert.That(ArgusProblemDetails.InternalServerError().StatusCode, Is.EqualTo(ArgusStatusCode.InternalServerError));
            Assert.That(ArgusProblemDetails.ServiceUnavailable().StatusCode, Is.EqualTo(ArgusStatusCode.ServiceUnavailable));
        }

        [Test]
        public void Verify_that_BadRequest_carries_detail_and_extensions()
        {
            var response = ArgusProblemDetails.BadRequest("Validation failed", new Dictionary<string, object> { ["field"] = "name" });

            Assert.That(ArgusProblemDetails.TryRead(response, out var problem), Is.True);
            Assert.That(problem.Status, Is.EqualTo(400));
            Assert.That(problem.Detail, Is.EqualTo("Validation failed"));
            Assert.That(((JsonElement)problem.Extensions["field"]).GetString(), Is.EqualTo("name"));
        }

        [Test]
        public void Verify_that_TryRead_returns_false_when_content_type_is_not_problem_json()
        {
            var response = new ArgusResponse
            {
                StatusCode = ArgusStatusCode.BadRequest,
                Body = "{\"status\":400}"
            };
            response.Headers[ArgusHeaderNames.ContentType] = "text/plain";

            Assert.That(ArgusProblemDetails.TryRead(response, out var problem), Is.False);
            Assert.That(problem, Is.Null);
        }

        [Test]
        public void Verify_that_TryRead_returns_false_for_null_response()
        {
            Assert.That(ArgusProblemDetails.TryRead(null, out var problem), Is.False);
            Assert.That(problem, Is.Null);
        }

        [Test]
        public void Verify_that_problem_details_response_survives_a_wire_round_trip()
        {
            var original = ArgusProblemDetails.NotFound("Item 42 not found", new Dictionary<string, object> { ["itemId"] = 42 });
            var serializer = new ArgusResponseSerializer();

            var result = serializer.Read(serializer.Write(original));

            Assert.That(result.StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));
            Assert.That(result.Headers[ArgusHeaderNames.ContentType], Is.EqualTo(ArgusProblemDetails.ContentType));
            Assert.That(ArgusProblemDetails.TryRead(result, out var problem), Is.True);
            Assert.That(problem.Detail, Is.EqualTo("Item 42 not found"));
            Assert.That(((JsonElement)problem.Extensions["itemId"]).GetInt32(), Is.EqualTo(42));
        }

        [Test]
        public void Verify_that_ArgusContext_Problem_sets_response_with_correlation_token_as_instance()
        {
            var request = new ArgusRequest { Verb = ArgusVerb.GET, Route = "/items/42" };
            var context = new ArgusContext(request, CancellationToken.None);

            var response = context.Problem(ArgusStatusCode.NotFound, "Item 42 not found");

            Assert.That(context.Response, Is.SameAs(response));
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));
            Assert.That(ArgusProblemDetails.TryRead(response, out var problem), Is.True);
            Assert.That(problem.Instance, Is.EqualTo(request.CorrelationToken.ToString()));
            Assert.That(problem.Detail, Is.EqualTo("Item 42 not found"));
        }
    }
}
