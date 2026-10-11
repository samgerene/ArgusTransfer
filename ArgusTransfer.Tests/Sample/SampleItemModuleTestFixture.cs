// ------------------------------------------------------------------------------------------------
// <copyright file="SampleItemModuleTestFixture.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests.Sample
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;

    using ArgusTransfer.Client;
    using ArgusTransfer.Protocol;
    using ArgusTransfer.Sample;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="SampleItemModule"/> class, sent over a pipe to a host that is configured as
    /// the sample configures it
    /// </summary>
    [TestFixture]
    public class SampleItemModuleTestFixture
    {
        /// <summary>
        /// JSON options matching the camelCase JSON of the sample
        /// </summary>
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        /// <summary>
        /// The host of the sample server
        /// </summary>
        private IHost host;

        /// <summary>
        /// The client the sample registers
        /// </summary>
        private IArgusClient client;

        [SetUp]
        public async Task SetUp()
        {
            var pipeName = $"argus-sample-module-test-{Guid.NewGuid():N}";

            this.host = new HostBuilder()
                .ConfigureServices(services => Program.ConfigureServices(services, pipeName))
                .Build();

            await this.host.StartAsync();

            this.client = this.host.Services.GetRequiredService<IArgusClient>();
        }

        [TearDown]
        public async Task TearDown()
        {
            this.client.Dispose();

            await this.host.StopAsync();
            this.host.Dispose();
        }

        [Test]
        public async Task Verify_that_listing_the_items_needs_no_credentials_and_returns_JSON()
        {
            var response = await this.client.SendAsync(CreateRequest(ArgusVerb.GET, "/sampleitems", authenticate: false));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(response.Headers[ArgusHeaderNames.ContentType], Is.EqualTo("application/json"));
            Assert.That(() => JsonSerializer.Deserialize<SampleItem[]>(response.Body, SerializerOptions), Throws.Nothing);
        }

        [Test]
        public async Task Verify_that_creating_an_item_without_credentials_returns_Unauthorized_problem_details()
        {
            var response = await this.client.SendAsync(CreateRequest(ArgusVerb.POST, "/sampleitems", "{\"name\":\"Widget\"}", authenticate: false));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Unauthorized));
            Assert.That(ArgusProblemDetails.TryRead(response, out _), Is.True);
        }

        [Test]
        public async Task Verify_that_a_wrong_api_key_returns_Unauthorized()
        {
            var request = CreateRequest(ArgusVerb.GET, $"/sampleitems/{Guid.NewGuid()}", authenticate: false);
            request.SetAuthorization(SampleApiKeyAuthenticationHandler.Scheme, "not-the-key");

            var response = await this.client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Unauthorized));
        }

        [Test]
        public async Task Verify_that_another_scheme_with_the_api_key_returns_Unauthorized()
        {
            var request = CreateRequest(ArgusVerb.GET, $"/sampleitems/{Guid.NewGuid()}", authenticate: false);
            request.SetAuthorization("Bearer", SampleApiKeyAuthenticationHandler.SampleApiKey);

            var response = await this.client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Unauthorized));
        }

        [TestCase(ArgusVerb.GET)]
        [TestCase(ArgusVerb.PUT)]
        [TestCase(ArgusVerb.DELETE)]
        public async Task Verify_that_every_route_except_the_list_requires_credentials(ArgusVerb verb)
        {
            var body = verb == ArgusVerb.PUT ? "{\"name\":\"Widget\"}" : null;

            var response = await this.client.SendAsync(CreateRequest(verb, $"/sampleitems/{Guid.NewGuid()}", body, authenticate: false));

            // Unauthorized and not NotFound: the request is rejected before the handler looks for the item
            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Unauthorized));
        }

        [TestCase("{ \"name\": ", ArgusStatusCode.BadRequest)]
        [TestCase("null", ArgusStatusCode.BadRequest)]
        [TestCase("{\"description\":\"no name\"}", ArgusStatusCode.UnprocessableEntity)]
        public async Task Verify_that_updating_an_item_with_an_invalid_body_returns_a_problem_and_leaves_the_item_unchanged(string body, ArgusStatusCode expectedStatusCode)
        {
            var response = await this.client.SendAsync(CreateRequest(ArgusVerb.POST, "/sampleitems", "{\"name\":\"Widget\",\"description\":\"original\"}"));
            var created = JsonSerializer.Deserialize<SampleItem>(response.Body, SerializerOptions);
            var route = $"/sampleitems/{created.Id}";

            response = await this.client.SendAsync(CreateRequest(ArgusVerb.PUT, route, body));

            Assert.That(response.StatusCode, Is.EqualTo(expectedStatusCode));
            Assert.That(ArgusProblemDetails.TryRead(response, out _), Is.True);

            response = await this.client.SendAsync(CreateRequest(ArgusVerb.GET, route));
            var read = JsonSerializer.Deserialize<SampleItem>(response.Body, SerializerOptions);

            Assert.That(read.Name, Is.EqualTo("Widget"));
            Assert.That(read.Description, Is.EqualTo("original"));

            await this.client.SendAsync(CreateRequest(ArgusVerb.DELETE, route));
        }

        [TestCase("")]
        [TestCase("{ \"name\": ")]
        [TestCase("not json")]
        [TestCase("null")]
        public async Task Verify_that_a_malformed_body_returns_BadRequest_problem_details_and_not_a_server_error(string body)
        {
            var response = await this.client.SendAsync(CreateRequest(ArgusVerb.POST, "/sampleitems", body));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.BadRequest));
            Assert.That(ArgusProblemDetails.TryRead(response, out var problem), Is.True);
            Assert.That(problem.Detail, Is.Not.Empty);
        }

        [Test]
        public async Task Verify_that_an_item_without_a_name_returns_UnprocessableEntity()
        {
            var response = await this.client.SendAsync(CreateRequest(ArgusVerb.POST, "/sampleitems", "{\"description\":\"no name\"}"));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.UnprocessableEntity));
            Assert.That(ArgusProblemDetails.TryRead(response, out _), Is.True);
        }

        [Test]
        public async Task Verify_that_an_item_can_be_created_read_updated_and_deleted()
        {
            var response = await this.client.SendAsync(CreateRequest(ArgusVerb.POST, "/sampleitems", "{\"name\":\"Widget\",\"description\":\"first\"}"));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Created));
            Assert.That(response.Headers[ArgusHeaderNames.ContentType], Is.EqualTo("application/json"));

            var created = JsonSerializer.Deserialize<SampleItem>(response.Body, SerializerOptions);

            Assert.That(created.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(created.Name, Is.EqualTo("Widget"));

            var route = $"/sampleitems/{created.Id}";

            response = await this.client.SendAsync(CreateRequest(ArgusVerb.PUT, route, "{\"name\":\"Updated\",\"description\":\"second\"}"));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));

            response = await this.client.SendAsync(CreateRequest(ArgusVerb.GET, route));
            var read = JsonSerializer.Deserialize<SampleItem>(response.Body, SerializerOptions);

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));
            Assert.That(read.Id, Is.EqualTo(created.Id));
            Assert.That(read.Name, Is.EqualTo("Updated"));
            Assert.That(read.Description, Is.EqualTo("second"));

            response = await this.client.SendAsync(CreateRequest(ArgusVerb.DELETE, route));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Ok));

            response = await this.client.SendAsync(CreateRequest(ArgusVerb.GET, route));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));
            Assert.That(ArgusProblemDetails.TryRead(response, out var problem), Is.True);
            Assert.That(problem.Detail, Does.Contain(created.Id.ToString()));
        }

        [TestCase(ArgusVerb.PUT)]
        [TestCase(ArgusVerb.DELETE)]
        public async Task Verify_that_changing_an_unknown_item_returns_NotFound(ArgusVerb verb)
        {
            var body = verb == ArgusVerb.PUT ? "{\"name\":\"Widget\"}" : null;

            var response = await this.client.SendAsync(CreateRequest(verb, $"/sampleitems/{Guid.NewGuid()}", body));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.NotFound));
        }

        [Test]
        public async Task Verify_that_a_body_large_enough_to_be_compressed_round_trips()
        {
            // Both sides of the sample have compression enabled; it applies from 1 KB
            var description = new string('d', 10_000);

            var response = await this.client.SendAsync(CreateRequest(ArgusVerb.POST, "/sampleitems", $"{{\"name\":\"Large\",\"description\":\"{description}\"}}"));

            Assert.That(response.StatusCode, Is.EqualTo(ArgusStatusCode.Created));

            var created = JsonSerializer.Deserialize<SampleItem>(response.Body, SerializerOptions);

            Assert.That(created.Description, Is.EqualTo(description));

            await this.client.SendAsync(CreateRequest(ArgusVerb.DELETE, $"/sampleitems/{created.Id}"));
        }

        /// <summary>
        /// Creates a request the way the sample client does
        /// </summary>
        /// <param name="verb">
        /// The <see cref="ArgusVerb"/> of the request
        /// </param>
        /// <param name="route">
        /// The route of the request
        /// </param>
        /// <param name="json">
        /// The body, or <c>null</c> for a request without a body
        /// </param>
        /// <param name="authenticate">
        /// <c>true</c> to send the API key of the sample
        /// </param>
        /// <returns>
        /// The <see cref="ArgusRequest"/>
        /// </returns>
        private static ArgusRequest CreateRequest(ArgusVerb verb, string route, string json = null, bool authenticate = true)
        {
            var request = new ArgusRequest { Verb = verb, Route = route, Accept = "application/json" };

            if (json != null)
            {
                request.Body = json;
                request.Headers[ArgusHeaderNames.ContentType] = "application/json";
            }

            if (authenticate)
            {
                request.SetAuthorization(SampleApiKeyAuthenticationHandler.Scheme, SampleApiKeyAuthenticationHandler.SampleApiKey);
            }

            return request;
        }
    }
}
