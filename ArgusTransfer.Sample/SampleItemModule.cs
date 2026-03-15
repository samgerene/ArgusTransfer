// -------------------------------------------------------------------------------------------------
//   <copyright file="SampleItemModule.cs">
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

namespace ArgusTransfer.Sample
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;

    /// <summary>
    /// Argus module that registers CRUD routes for <see cref="SampleItem"/> resources
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SampleItemModule : IArgusModule
    {
        /// <summary>
        /// In-memory store for <see cref="SampleItem"/> instances.
        /// Static because modules are transient but route handler lambdas must
        /// share state across requests.
        /// </summary>
        private static readonly ConcurrentDictionary<Guid, SampleItem> Store = new ConcurrentDictionary<Guid, SampleItem>();

        /// <summary>
        /// JSON serializer options using camelCase property naming
        /// </summary>
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Registers CRUD routes for the <see cref="SampleItem"/> resource
        /// </summary>
        /// <param name="app">
        /// The <see cref="IArgusRouteBuilder"/> to register routes with
        /// </param>
        public void AddRoutes(IArgusRouteBuilder app)
        {
            app.MapGet("/sampleitems", (request, routeValues) =>
            {
                var items = Store.Values.ToList();
                var json = JsonSerializer.Serialize(items, SerializerOptions);

                return Task.FromResult(new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok,
                    Body = json
                });
            });

            app.MapGet("/sampleitems/{id:Guid}", (request, routeValues) =>
            {
                var id = Guid.Parse(routeValues["id"]);

                if (Store.TryGetValue(id, out var item))
                {
                    return Task.FromResult(new ArgusResponse
                    {
                        CorrelationToken = request.CorrelationToken,
                        StatusCode = ArgusStatusCode.Ok,
                        Body = JsonSerializer.Serialize(item, SerializerOptions)
                    });
                }

                return Task.FromResult(new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.NotFound
                });
            });

            app.MapPost("/sampleitems", (request, routeValues) =>
            {
                var item = JsonSerializer.Deserialize<SampleItem>(request.Body, SerializerOptions);
                item.Id = Guid.NewGuid();
                Store[item.Id] = item;

                return Task.FromResult(new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Created,
                    Body = JsonSerializer.Serialize(item, SerializerOptions)
                });
            });

            app.MapPut("/sampleitems/{id:Guid}", (request, routeValues) =>
            {
                var id = Guid.Parse(routeValues["id"]);

                if (!Store.ContainsKey(id))
                {
                    return Task.FromResult(new ArgusResponse
                    {
                        CorrelationToken = request.CorrelationToken,
                        StatusCode = ArgusStatusCode.NotFound
                    });
                }

                var item = JsonSerializer.Deserialize<SampleItem>(request.Body, SerializerOptions);
                item.Id = id;
                Store[id] = item;

                return Task.FromResult(new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.Ok,
                    Body = JsonSerializer.Serialize(item, SerializerOptions)
                });
            });

            app.MapDelete("/sampleitems/{id:Guid}", (request, routeValues) =>
            {
                var id = Guid.Parse(routeValues["id"]);

                if (Store.TryRemove(id, out var item))
                {
                    return Task.FromResult(new ArgusResponse
                    {
                        CorrelationToken = request.CorrelationToken,
                        StatusCode = ArgusStatusCode.Ok,
                        Body = JsonSerializer.Serialize(item, SerializerOptions)
                    });
                }

                return Task.FromResult(new ArgusResponse
                {
                    CorrelationToken = request.CorrelationToken,
                    StatusCode = ArgusStatusCode.NotFound
                });
            });
        }
    }
}
