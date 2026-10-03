// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusProblemDetails.cs">
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

namespace ArgusTransfer.Protocol
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A machine-readable error body for Argus error responses, modeled on RFC 7807 Problem Details.
    /// Serialized as JSON with the <see cref="ContentType"/> "application/problem+json"; entries in
    /// <see cref="Extensions"/> are written as top-level members alongside the standard members
    /// </summary>
    public class ArgusProblemDetails
    {
        /// <summary>
        /// The MIME content type for problem details bodies: "application/problem+json"
        /// </summary>
        public const string ContentType = "application/problem+json";

        /// <summary>
        /// The default <see cref="Type"/> value, indicating that the problem has no additional
        /// semantics beyond those of the status code
        /// </summary>
        public const string DefaultType = "about:blank";

        /// <summary>
        /// The <see cref="JsonSerializerOptions"/> used to serialize and deserialize problem details
        /// </summary>
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Gets or sets a URI or identifier for the error category
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets a short, human-readable summary of the error category
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the numeric status code of the response carrying this problem
        /// </summary>
        [JsonPropertyName("status")]
        public int Status { get; set; }

        /// <summary>
        /// Gets or sets a human-readable explanation specific to this occurrence of the problem
        /// </summary>
        [JsonPropertyName("detail")]
        public string Detail { get; set; }

        /// <summary>
        /// Gets or sets an identifier for this specific occurrence, typically the correlation token of the request
        /// </summary>
        [JsonPropertyName("instance")]
        public string Instance { get; set; }

        /// <summary>
        /// Gets or sets additional context for the problem, serialized as top-level JSON members.
        /// After deserialization the values are <see cref="JsonElement"/> instances
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> Extensions { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates an <see cref="ArgusProblemDetails"/> for the specified status code, using the
        /// status code reason phrase as <see cref="Title"/> and <see cref="DefaultType"/> as <see cref="Type"/>
        /// unless overridden
        /// </summary>
        /// <param name="statusCode">
        /// The <see cref="ArgusStatusCode"/> of the problem
        /// </param>
        /// <param name="detail">
        /// A human-readable explanation specific to this occurrence of the problem
        /// </param>
        /// <param name="extensions">
        /// Optional additional context to include as top-level members
        /// </param>
        /// <param name="title">
        /// An optional title; defaults to the reason phrase of <paramref name="statusCode"/>
        /// </param>
        /// <param name="type">
        /// An optional error category identifier; defaults to <see cref="DefaultType"/>
        /// </param>
        /// <param name="instance">
        /// An optional identifier for this occurrence, such as the request correlation token
        /// </param>
        /// <returns>
        /// The created <see cref="ArgusProblemDetails"/>
        /// </returns>
        public static ArgusProblemDetails Create(
            ArgusStatusCode statusCode,
            string detail = null,
            IDictionary<string, object> extensions = null,
            string title = null,
            string type = null,
            string instance = null)
        {
            var problem = new ArgusProblemDetails
            {
                Type = type ?? DefaultType,
                Title = title ?? statusCode.ToReasonPhrase(),
                Status = (int)statusCode,
                Detail = detail,
                Instance = instance
            };

            if (extensions != null)
            {
                foreach (var extension in extensions)
                {
                    problem.Extensions[extension.Key] = extension.Value;
                }
            }

            return problem;
        }

        /// <summary>
        /// Creates an <see cref="ArgusResponse"/> with status <see cref="ArgusStatusCode.BadRequest"/> and a problem details body
        /// </summary>
        /// <param name="detail">
        /// A human-readable explanation specific to this occurrence of the problem
        /// </param>
        /// <param name="extensions">
        /// Optional additional context to include as top-level members
        /// </param>
        /// <returns>
        /// The created <see cref="ArgusResponse"/>
        /// </returns>
        public static ArgusResponse BadRequest(string detail = null, IDictionary<string, object> extensions = null)
        {
            return Create(ArgusStatusCode.BadRequest, detail, extensions).ToResponse();
        }

        /// <summary>
        /// Creates an <see cref="ArgusResponse"/> with status <see cref="ArgusStatusCode.Unauthorized"/> and a problem details body
        /// </summary>
        /// <param name="detail">
        /// A human-readable explanation specific to this occurrence of the problem
        /// </param>
        /// <param name="extensions">
        /// Optional additional context to include as top-level members
        /// </param>
        /// <returns>
        /// The created <see cref="ArgusResponse"/>
        /// </returns>
        public static ArgusResponse Unauthorized(string detail = null, IDictionary<string, object> extensions = null)
        {
            return Create(ArgusStatusCode.Unauthorized, detail, extensions).ToResponse();
        }

        /// <summary>
        /// Creates an <see cref="ArgusResponse"/> with status <see cref="ArgusStatusCode.Forbidden"/> and a problem details body
        /// </summary>
        /// <param name="detail">
        /// A human-readable explanation specific to this occurrence of the problem
        /// </param>
        /// <param name="extensions">
        /// Optional additional context to include as top-level members
        /// </param>
        /// <returns>
        /// The created <see cref="ArgusResponse"/>
        /// </returns>
        public static ArgusResponse Forbidden(string detail = null, IDictionary<string, object> extensions = null)
        {
            return Create(ArgusStatusCode.Forbidden, detail, extensions).ToResponse();
        }

        /// <summary>
        /// Creates an <see cref="ArgusResponse"/> with status <see cref="ArgusStatusCode.NotFound"/> and a problem details body
        /// </summary>
        /// <param name="detail">
        /// A human-readable explanation specific to this occurrence of the problem
        /// </param>
        /// <param name="extensions">
        /// Optional additional context to include as top-level members
        /// </param>
        /// <returns>
        /// The created <see cref="ArgusResponse"/>
        /// </returns>
        public static ArgusResponse NotFound(string detail = null, IDictionary<string, object> extensions = null)
        {
            return Create(ArgusStatusCode.NotFound, detail, extensions).ToResponse();
        }

        /// <summary>
        /// Creates an <see cref="ArgusResponse"/> with status <see cref="ArgusStatusCode.Conflict"/> and a problem details body
        /// </summary>
        /// <param name="detail">
        /// A human-readable explanation specific to this occurrence of the problem
        /// </param>
        /// <param name="extensions">
        /// Optional additional context to include as top-level members
        /// </param>
        /// <returns>
        /// The created <see cref="ArgusResponse"/>
        /// </returns>
        public static ArgusResponse Conflict(string detail = null, IDictionary<string, object> extensions = null)
        {
            return Create(ArgusStatusCode.Conflict, detail, extensions).ToResponse();
        }

        /// <summary>
        /// Creates an <see cref="ArgusResponse"/> with status <see cref="ArgusStatusCode.UnprocessableEntity"/> and a problem details body
        /// </summary>
        /// <param name="detail">
        /// A human-readable explanation specific to this occurrence of the problem
        /// </param>
        /// <param name="extensions">
        /// Optional additional context to include as top-level members
        /// </param>
        /// <returns>
        /// The created <see cref="ArgusResponse"/>
        /// </returns>
        public static ArgusResponse UnprocessableEntity(string detail = null, IDictionary<string, object> extensions = null)
        {
            return Create(ArgusStatusCode.UnprocessableEntity, detail, extensions).ToResponse();
        }

        /// <summary>
        /// Creates an <see cref="ArgusResponse"/> with status <see cref="ArgusStatusCode.InternalServerError"/> and a problem details body
        /// </summary>
        /// <param name="detail">
        /// A human-readable explanation specific to this occurrence of the problem
        /// </param>
        /// <param name="extensions">
        /// Optional additional context to include as top-level members
        /// </param>
        /// <returns>
        /// The created <see cref="ArgusResponse"/>
        /// </returns>
        public static ArgusResponse InternalServerError(string detail = null, IDictionary<string, object> extensions = null)
        {
            return Create(ArgusStatusCode.InternalServerError, detail, extensions).ToResponse();
        }

        /// <summary>
        /// Creates an <see cref="ArgusResponse"/> with status <see cref="ArgusStatusCode.ServiceUnavailable"/> and a problem details body
        /// </summary>
        /// <param name="detail">
        /// A human-readable explanation specific to this occurrence of the problem
        /// </param>
        /// <param name="extensions">
        /// Optional additional context to include as top-level members
        /// </param>
        /// <returns>
        /// The created <see cref="ArgusResponse"/>
        /// </returns>
        public static ArgusResponse ServiceUnavailable(string detail = null, IDictionary<string, object> extensions = null)
        {
            return Create(ArgusStatusCode.ServiceUnavailable, detail, extensions).ToResponse();
        }

        /// <summary>
        /// Deserializes an <see cref="ArgusProblemDetails"/> from its JSON representation
        /// </summary>
        /// <param name="json">
        /// The JSON string to deserialize
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusProblemDetails"/>
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="json"/> is <c>null</c>
        /// </exception>
        /// <exception cref="JsonException">
        /// Thrown when <paramref name="json"/> is not a valid problem details JSON object
        /// </exception>
        public static ArgusProblemDetails FromJson(string json)
        {
            ArgumentNullException.ThrowIfNull(json);

            return JsonSerializer.Deserialize<ArgusProblemDetails>(json, SerializerOptions)
                ?? throw new JsonException("The JSON does not represent a problem details object.");
        }

        /// <summary>
        /// Attempts to deserialize an <see cref="ArgusProblemDetails"/> from its JSON representation
        /// </summary>
        /// <param name="json">
        /// The JSON string to deserialize
        /// </param>
        /// <param name="problemDetails">
        /// When this method returns <c>true</c>, contains the deserialized <see cref="ArgusProblemDetails"/>;
        /// otherwise <c>null</c>
        /// </param>
        /// <returns>
        /// <c>true</c> if <paramref name="json"/> was successfully deserialized; otherwise <c>false</c>
        /// </returns>
        public static bool TryParse(string json, out ArgusProblemDetails problemDetails)
        {
            problemDetails = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                problemDetails = JsonSerializer.Deserialize<ArgusProblemDetails>(json, SerializerOptions);
                return problemDetails != null;
            }
            catch (JsonException)
            {
                problemDetails = null;
                return false;
            }
        }

        /// <summary>
        /// Attempts to read an <see cref="ArgusProblemDetails"/> from the body of an <see cref="ArgusResponse"/>.
        /// Succeeds only when the response <see cref="ArgusHeaderNames.ContentType"/> header is <see cref="ContentType"/>
        /// and the body is valid problem details JSON
        /// </summary>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to read from
        /// </param>
        /// <param name="problemDetails">
        /// When this method returns <c>true</c>, contains the deserialized <see cref="ArgusProblemDetails"/>;
        /// otherwise <c>null</c>
        /// </param>
        /// <returns>
        /// <c>true</c> if the response carries a problem details body; otherwise <c>false</c>
        /// </returns>
        public static bool TryRead(ArgusResponse response, out ArgusProblemDetails problemDetails)
        {
            problemDetails = null;

            if (response == null
                || !response.Headers.TryGetValue(ArgusHeaderNames.ContentType, out var contentType)
                || !string.Equals(contentType, ContentType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryParse(response.Body, out problemDetails);
        }

        /// <summary>
        /// Serializes this <see cref="ArgusProblemDetails"/> to its JSON representation.
        /// Members with a <c>null</c> value are omitted
        /// </summary>
        /// <returns>
        /// The JSON string
        /// </returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, SerializerOptions);
        }

        /// <summary>
        /// Creates an <see cref="ArgusResponse"/> carrying this problem details as its body, with the
        /// <see cref="ArgusHeaderNames.ContentType"/> header set to <see cref="ContentType"/>.
        /// The response status code is taken from <see cref="Status"/>
        /// </summary>
        /// <returns>
        /// The created <see cref="ArgusResponse"/>
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="Status"/> is not a defined <see cref="ArgusStatusCode"/>
        /// </exception>
        public ArgusResponse ToResponse()
        {
            if (!ArgusStatusCodeExtensions.TryParse(this.Status, out var statusCode))
            {
                throw new InvalidOperationException($"Status {this.Status} is not a defined {nameof(ArgusStatusCode)}.");
            }

            var response = new ArgusResponse
            {
                StatusCode = statusCode,
                Body = this.ToJson()
            };

            response.Headers[ArgusHeaderNames.ContentType] = ContentType;

            return response;
        }
    }
}
