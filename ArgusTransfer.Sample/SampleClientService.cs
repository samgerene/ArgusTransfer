// -------------------------------------------------------------------------------------------------
//   <copyright file="SampleClientService.cs" company="Sam Gerene">
//
//     Copyright (c) 2026 Sam Gerene
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
    /// Background service that acts as a CLI client, executing all CRUD verbs
    /// sequentially against the sample pipe server and printing results
    /// </summary>
    public class SampleClientService : BackgroundService
    {
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
        /// Initializes a new instance of the <see cref="SampleClientService"/> class
        /// </summary>
        /// <param name="lifetime">
        /// The <see cref="IHostApplicationLifetime"/> used to stop the application after the demo completes
        /// </param>
        public SampleClientService(IHostApplicationLifetime lifetime)
        {
            this.lifetime = lifetime;
        }

        /// <summary>
        /// Executes the sample CRUD demo sequence
        /// </summary>
        /// <param name="stoppingToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// An awaitable <see cref="Task"/>
        /// </returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(1000, stoppingToken);

            Console.WriteLine("=== ArgusTransfer Sample ===");
            Console.WriteLine();

            using (var client = new ArgusClient("sample"))
            {
                // 1. GET /sampleitems — empty list
                Console.WriteLine("--- Step 1: List all items (expect empty) ---");
                var response = await client.SendAsync(new ArgusRequest
                {
                    Verb = ArgusVerb.GET,
                    Route = "/sampleitems"
                }, stoppingToken);
                PrintResponse("GET", "/sampleitems", response);

                // 2. POST /sampleitems — create a widget
                Console.WriteLine("--- Step 2: Create a new item ---");
                var newItem = new { name = "Widget", description = "A sample widget" };
                response = await client.SendAsync(new ArgusRequest
                {
                    Verb = ArgusVerb.POST,
                    Route = "/sampleitems",
                    Body = JsonSerializer.Serialize(newItem, SerializerOptions)
                }, stoppingToken);
                PrintResponse("POST", "/sampleitems", response);

                var created = JsonSerializer.Deserialize<SampleItem>(response.Body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                var id = created.Id;

                // 3. GET /sampleitems/{id} — read it back
                Console.WriteLine("--- Step 3: Read the created item ---");
                response = await client.SendAsync(new ArgusRequest
                {
                    Verb = ArgusVerb.GET,
                    Route = $"/sampleitems/{id}"
                }, stoppingToken);
                PrintResponse("GET", $"/sampleitems/{id}", response);

                // 4. PUT /sampleitems/{id} — update it
                Console.WriteLine("--- Step 4: Update the item ---");
                var updatedItem = new { name = "Updated Widget", description = "An updated sample widget" };
                response = await client.SendAsync(new ArgusRequest
                {
                    Verb = ArgusVerb.PUT,
                    Route = $"/sampleitems/{id}",
                    Body = JsonSerializer.Serialize(updatedItem, SerializerOptions)
                }, stoppingToken);
                PrintResponse("PUT", $"/sampleitems/{id}", response);

                // 5. GET /sampleitems/{id} — verify update
                Console.WriteLine("--- Step 5: Verify the update ---");
                response = await client.SendAsync(new ArgusRequest
                {
                    Verb = ArgusVerb.GET,
                    Route = $"/sampleitems/{id}"
                }, stoppingToken);
                PrintResponse("GET", $"/sampleitems/{id}", response);

                // 6. DELETE /sampleitems/{id} — delete it
                Console.WriteLine("--- Step 6: Delete the item ---");
                response = await client.SendAsync(new ArgusRequest
                {
                    Verb = ArgusVerb.DELETE,
                    Route = $"/sampleitems/{id}"
                }, stoppingToken);
                PrintResponse("DELETE", $"/sampleitems/{id}", response);

                // 7. GET /sampleitems — verify empty list
                Console.WriteLine("--- Step 7: List all items (expect empty) ---");
                response = await client.SendAsync(new ArgusRequest
                {
                    Verb = ArgusVerb.GET,
                    Route = "/sampleitems"
                }, stoppingToken);
                PrintResponse("GET", "/sampleitems", response);
            }

            Console.WriteLine("=== Demo Complete ===");

            this.lifetime.StopApplication();
        }

        /// <summary>
        /// Prints the verb, route, status code, and response body to the console
        /// </summary>
        /// <param name="verb">The HTTP verb used</param>
        /// <param name="route">The route requested</param>
        /// <param name="response">The <see cref="ArgusResponse"/> received</param>
        private static void PrintResponse(string verb, string route, ArgusResponse response)
        {
            Console.WriteLine($"{verb} {route} -> {(int)response.StatusCode} {response.StatusCode}");

            if (!string.IsNullOrEmpty(response.Body))
            {
                Console.WriteLine(response.Body);
            }

            Console.WriteLine();
        }
    }
}
