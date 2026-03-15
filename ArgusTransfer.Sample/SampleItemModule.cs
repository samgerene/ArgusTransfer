// -------------------------------------------------------------------------------------------------
//   <copyright file="SampleItemModule.cs">
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
            app.MapGet("/sampleitems", context =>
            {
                var items = Store.Values.ToList();
                var json = JsonSerializer.Serialize(items, SerializerOptions);

                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.Ok,
                    Body = json
                };

                return Task.CompletedTask;
            });

            app.MapGet("/sampleitems/{id:Guid}", context =>
            {
                var id = Guid.Parse(context.RouteValues["id"]);

                if (Store.TryGetValue(id, out var item))
                {
                    context.Response = new ArgusResponse
                    {
    
                        StatusCode = ArgusStatusCode.Ok,
                        Body = JsonSerializer.Serialize(item, SerializerOptions)
                    };

                    return Task.CompletedTask;
                }

                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.NotFound
                };

                return Task.CompletedTask;
            });

            app.MapPost("/sampleitems", context =>
            {
                var item = JsonSerializer.Deserialize<SampleItem>(context.Request.Body, SerializerOptions);
                item.Id = Guid.NewGuid();
                Store[item.Id] = item;

                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.Created,
                    Body = JsonSerializer.Serialize(item, SerializerOptions)
                };

                return Task.CompletedTask;
            });

            app.MapPut("/sampleitems/{id:Guid}", context =>
            {
                var id = Guid.Parse(context.RouteValues["id"]);

                if (!Store.ContainsKey(id))
                {
                    context.Response = new ArgusResponse
                    {
    
                        StatusCode = ArgusStatusCode.NotFound
                    };

                    return Task.CompletedTask;
                }

                var item = JsonSerializer.Deserialize<SampleItem>(context.Request.Body, SerializerOptions);
                item.Id = id;
                Store[id] = item;

                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.Ok,
                    Body = JsonSerializer.Serialize(item, SerializerOptions)
                };

                return Task.CompletedTask;
            });

            app.MapDelete("/sampleitems/{id:Guid}", context =>
            {
                var id = Guid.Parse(context.RouteValues["id"]);

                if (Store.TryRemove(id, out var item))
                {
                    context.Response = new ArgusResponse
                    {
    
                        StatusCode = ArgusStatusCode.Ok,
                        Body = JsonSerializer.Serialize(item, SerializerOptions)
                    };

                    return Task.CompletedTask;
                }

                context.Response = new ArgusResponse
                {

                    StatusCode = ArgusStatusCode.NotFound
                };

                return Task.CompletedTask;
            });
        }
    }
}
