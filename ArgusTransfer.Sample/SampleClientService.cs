// ------------------------------------------------------------------------------------------------
// <copyright file="SampleClientService.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Sample
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Client;
    using ArgusTransfer.Protocol;

    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Background service that acts as a CLI client: it sends a sequence of requests to the sample pipe server,
    /// prints each result and checks that the server answered with the expected status
    /// </summary>
    public class SampleClientService : BackgroundService
    {
        /// <summary>
        /// The <see cref="IArgusClient"/> used to send requests to the pipe server
        /// </summary>
        private readonly IArgusClient client;

        /// <summary>
        /// The <see cref="IHostApplicationLifetime"/> used to stop the application when done
        /// </summary>
        private readonly IHostApplicationLifetime lifetime;

        /// <summary>
        /// JSON serializer options using camelCase property naming
        /// </summary>
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        /// <summary>
        /// JSON deserializer options using case-insensitive property matching
        /// </summary>
        private static readonly JsonSerializerOptions DeserializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="SampleClientService"/> class
        /// </summary>
        /// <param name="client">
        /// The <see cref="IArgusClient"/> used to send requests to the pipe server
        /// </param>
        /// <param name="lifetime">
        /// The <see cref="IHostApplicationLifetime"/> used to stop the application after the demo completes
        /// </param>
        public SampleClientService(IArgusClient client, IHostApplicationLifetime lifetime)
        {
            this.client = client;
            this.lifetime = lifetime;
        }

        /// <summary>
        /// Executes the sample demo sequence
        /// </summary>
        /// <param name="stoppingToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// An awaitable <see cref="Task"/>
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the server answers a step with another status than the expected one
        /// </exception>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // There is no need to wait for the pipe host: the client's connect waits for the pipe, within the request timeout
            Console.WriteLine("=== ArgusTransfer Sample ===");
            Console.WriteLine();

            Console.WriteLine("--- Step 1: List all items; this endpoint allows anonymous callers ---");
            var request = CreateRequest(ArgusVerb.GET, "/sampleitems", authenticate: false);
            await this.SendAsync(request, ArgusStatusCode.Ok, stoppingToken);

            Console.WriteLine("--- Step 2: Create an item without credentials (expect 401 problem details) ---");
            var newItem = new { name = "Widget", description = "A sample widget" };
            request = CreateRequest(ArgusVerb.POST, "/sampleitems", JsonSerializer.Serialize(newItem, SerializerOptions), authenticate: false);
            await this.SendAsync(request, ArgusStatusCode.Unauthorized, stoppingToken);

            Console.WriteLine("--- Step 3: Create an item from malformed JSON (expect 400 problem details) ---");
            request = CreateRequest(ArgusVerb.POST, "/sampleitems", "{ \"name\": ");
            await this.SendAsync(request, ArgusStatusCode.BadRequest, stoppingToken);

            Console.WriteLine("--- Step 4: Create a new item ---");
            request = CreateRequest(ArgusVerb.POST, "/sampleitems", JsonSerializer.Serialize(newItem, SerializerOptions));
            var response = await this.SendAsync(request, ArgusStatusCode.Created, stoppingToken);

            var created = JsonSerializer.Deserialize<SampleItem>(response.Body, DeserializerOptions);
            var id = created.Id;

            Console.WriteLine("--- Step 5: Read the created item ---");
            request = CreateRequest(ArgusVerb.GET, $"/sampleitems/{id}");
            await this.SendAsync(request, ArgusStatusCode.Ok, stoppingToken);

            Console.WriteLine("--- Step 6: Update the item ---");
            var updatedItem = new { name = "Updated Widget", description = "An updated sample widget" };
            request = CreateRequest(ArgusVerb.PUT, $"/sampleitems/{id}", JsonSerializer.Serialize(updatedItem, SerializerOptions));
            await this.SendAsync(request, ArgusStatusCode.Ok, stoppingToken);

            Console.WriteLine("--- Step 7: Delete the item ---");
            request = CreateRequest(ArgusVerb.DELETE, $"/sampleitems/{id}");
            await this.SendAsync(request, ArgusStatusCode.Ok, stoppingToken);

            Console.WriteLine("--- Step 8: Read the deleted item (expect 404 problem details) ---");
            request = CreateRequest(ArgusVerb.GET, $"/sampleitems/{id}");
            await this.SendAsync(request, ArgusStatusCode.NotFound, stoppingToken);

            Console.WriteLine("=== Demo Complete ===");

            this.lifetime.StopApplication();
        }

        /// <summary>
        /// Creates a request that asks for a JSON response, optionally with a JSON body and the credentials of the sample
        /// </summary>
        /// <param name="verb">
        /// The <see cref="ArgusVerb"/> of the request
        /// </param>
        /// <param name="route">
        /// The route of the request
        /// </param>
        /// <param name="json">
        /// The JSON body, or <c>null</c> for a request without a body
        /// </param>
        /// <param name="authenticate">
        /// <c>true</c> to send the API key of the sample in the <c>Authorization</c> header
        /// </param>
        /// <returns>
        /// The <see cref="ArgusRequest"/>
        /// </returns>
        private static ArgusRequest CreateRequest(ArgusVerb verb, string route, string json = null, bool authenticate = true)
        {
            var request = new ArgusRequest
            {
                Verb = verb,
                Route = route,
                Accept = JsonArgusBodySerializer.JsonContentType
            };

            if (json != null)
            {
                request.Body = json;
                request.Headers[ArgusHeaderNames.ContentType] = JsonArgusBodySerializer.JsonContentType;
            }

            if (authenticate)
            {
                request.SetAuthorization(SampleApiKeyAuthenticationHandler.Scheme, SampleApiKeyAuthenticationHandler.SampleApiKey);
            }

            return request;
        }

        /// <summary>
        /// Sends the request, prints the response and checks its status
        /// </summary>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> to send
        /// </param>
        /// <param name="expectedStatusCode">
        /// The <see cref="ArgusStatusCode"/> the server is expected to answer with
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task{ArgusResponse}"/> with the response of the server
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the server answers with another status than <paramref name="expectedStatusCode"/>
        /// </exception>
        private async Task<ArgusResponse> SendAsync(ArgusRequest request, ArgusStatusCode expectedStatusCode, CancellationToken cancellationToken)
        {
            var response = await this.client.SendAsync(request, cancellationToken: cancellationToken);

            PrintResponse(request, response);

            if (response.StatusCode != expectedStatusCode)
            {
                throw new InvalidOperationException(
                    $"{request.Verb} {request.Route} returned {(int)response.StatusCode} {response.StatusCode}; expected {(int)expectedStatusCode} {expectedStatusCode}.");
            }

            return response;
        }

        /// <summary>
        /// Prints the verb, route and status code, followed by the problem details of an error response or the body of
        /// any other response
        /// </summary>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> that was sent
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> received
        /// </param>
        private static void PrintResponse(ArgusRequest request, ArgusResponse response)
        {
            Console.WriteLine($"{request.Verb} {request.Route} -> {(int)response.StatusCode} {response.StatusCode}");

            if (ArgusProblemDetails.TryRead(response, out var problem))
            {
                Console.WriteLine($"{problem.Title}: {problem.Detail}");
            }
            else if (!string.IsNullOrEmpty(response.Body))
            {
                Console.WriteLine(response.Body);
            }

            Console.WriteLine();
        }
    }
}
