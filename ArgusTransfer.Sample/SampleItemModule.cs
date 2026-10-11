// ------------------------------------------------------------------------------------------------
// <copyright file="SampleItemModule.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Sample
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;

    using ArgusTransfer.Authentication;
    using ArgusTransfer.Protocol;
    using ArgusTransfer.Routing;

    /// <summary>
    /// Argus module that registers CRUD routes for <see cref="SampleItem"/> resources. Listing the items is open to
    /// every caller; the other routes require an authenticated caller. Successful responses are JSON, errors are
    /// <see cref="ArgusProblemDetails"/>.
    /// </summary>
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
            // Every endpoint requires an authenticated caller unless it opts out
            app.MapGet("/sampleitems", context =>
            {
                context.Response = CreateJsonResponse(ArgusStatusCode.Ok, Store.Values.ToList());

                return Task.CompletedTask;
            }).AllowAnonymous();

            app.MapGet("/sampleitems/{id:Guid}", context =>
            {
                var id = Guid.Parse(context.RouteValues["id"]);

                if (Store.TryGetValue(id, out var item))
                {
                    context.Response = CreateJsonResponse(ArgusStatusCode.Ok, item);
                }
                else
                {
                    NotFound(context, id);
                }

                return Task.CompletedTask;
            });

            app.MapPost("/sampleitems", context =>
            {
                if (TryReadItem(context, out var item))
                {
                    item.Id = Guid.NewGuid();
                    Store[item.Id] = item;

                    context.Response = CreateJsonResponse(ArgusStatusCode.Created, item);
                }

                return Task.CompletedTask;
            });

            app.MapPut("/sampleitems/{id:Guid}", context =>
            {
                var id = Guid.Parse(context.RouteValues["id"]);

                if (!Store.ContainsKey(id))
                {
                    NotFound(context, id);
                }
                else if (TryReadItem(context, out var item))
                {
                    item.Id = id;
                    Store[id] = item;

                    context.Response = CreateJsonResponse(ArgusStatusCode.Ok, item);
                }

                return Task.CompletedTask;
            });

            app.MapDelete("/sampleitems/{id:Guid}", context =>
            {
                var id = Guid.Parse(context.RouteValues["id"]);

                if (Store.TryRemove(id, out var item))
                {
                    context.Response = CreateJsonResponse(ArgusStatusCode.Ok, item);
                }
                else
                {
                    NotFound(context, id);
                }

                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Creates a response with a JSON body and the matching <c>Content-Type</c>
        /// </summary>
        /// <param name="statusCode">
        /// The <see cref="ArgusStatusCode"/> of the response
        /// </param>
        /// <param name="value">
        /// The value to serialize as the body
        /// </param>
        /// <returns>
        /// The <see cref="ArgusResponse"/>
        /// </returns>
        private static ArgusResponse CreateJsonResponse(ArgusStatusCode statusCode, object value)
        {
            var response = new ArgusResponse
            {
                StatusCode = statusCode,
                Body = JsonSerializer.Serialize(value, SerializerOptions)
            };

            response.Headers[ArgusHeaderNames.ContentType] = JsonArgusBodySerializer.JsonContentType;

            return response;
        }

        /// <summary>
        /// Answers the request with a 404 problem details response
        /// </summary>
        /// <param name="context">
        /// The <see cref="ArgusContext"/> of the request
        /// </param>
        /// <param name="id">
        /// The identifier that was not found
        /// </param>
        private static void NotFound(ArgusContext context, Guid id)
        {
            context.Problem(ArgusStatusCode.NotFound, $"No sample item with id {id} exists.");
        }

        /// <summary>
        /// Reads the <see cref="SampleItem"/> from the body of the request. A body that is not valid is the caller's
        /// mistake, so it is answered with a 400 or 422 problem details response; letting the exception escape
        /// would turn it into a 500.
        /// </summary>
        /// <param name="context">
        /// The <see cref="ArgusContext"/> of the request; its response is set when the body is not valid
        /// </param>
        /// <param name="item">
        /// The <see cref="SampleItem"/> that was read, or <c>null</c> when the body is not valid
        /// </param>
        /// <returns>
        /// <c>true</c> when the body holds a valid <see cref="SampleItem"/>; otherwise <c>false</c>
        /// </returns>
        private static bool TryReadItem(ArgusContext context, out SampleItem item)
        {
            item = null;

            try
            {
                item = JsonSerializer.Deserialize<SampleItem>(context.Request.Body ?? string.Empty, SerializerOptions);
            }
            catch (JsonException exception)
            {
                context.Problem(ArgusStatusCode.BadRequest, $"The request body is not valid JSON: {exception.Message}");

                return false;
            }

            if (item == null)
            {
                context.Problem(ArgusStatusCode.BadRequest, "The request body must be a JSON object that describes a sample item.");

                return false;
            }

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                item = null;
                context.Problem(ArgusStatusCode.UnprocessableEntity, "The name of a sample item must not be empty.");

                return false;
            }

            return true;
        }
    }
}
