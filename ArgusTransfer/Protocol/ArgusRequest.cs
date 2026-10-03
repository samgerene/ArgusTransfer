// -------------------------------------------------------------------------------------------------
//   <copyright file="HealthServiceMessage.cs">
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
    using System.Linq;

    /// <summary>
    /// The purpose of the <see cref="ArgusRequest"/> is to support a request-response
    /// protocol that uses HTTP like structure
    /// </summary>
    public class ArgusRequest : ArgusMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusRequest"/> class
        /// with default header values
        /// </summary>
        public ArgusRequest()
        {
            Headers[ArgusHeaderNames.Accept] = "text/plain";
        }

        /// <summary>
        /// The verb used to determine the type of operation
        /// </summary>
        public ArgusVerb Verb { get; set; }

        /// <summary>
        /// The route path for the request (e.g. "/healthendpoint",
        /// "/healthendpoint/{identifier:Guid}")
        /// </summary>
        public string Route { get; set; }

        /// <summary>
        /// Gets the query parameters parsed from the request URL
        /// </summary>
        public Dictionary<string, string> QueryParameters { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the preferred content type(s) for the response, as specified
        /// by the client via the Accept header (e.g. "application/json", "text/xml").
        /// This is a convenience accessor over <c>Headers[ArgusHeaderNames.Accept]</c>.
        /// </summary>
        public string Accept
        {
            get => Headers.TryGetValue(ArgusHeaderNames.Accept, out var value) ? value : null;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    Headers.Remove(ArgusHeaderNames.Accept);
                }
                else
                {
                    Headers[ArgusHeaderNames.Accept] = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the raw value of the <c>Authorization</c> header, in the form <c>{scheme} {parameter}</c>
        /// (e.g. "Bearer token123"). This is a convenience accessor over <c>Headers[ArgusHeaderNames.Authorization]</c>;
        /// setting <c>null</c> or an empty string removes the header. The value carries credentials and should not be logged.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when the value contains a carriage return or line feed, which would inject additional headers on the wire
        /// </exception>
        public string Authorization
        {
            get => Headers.TryGetValue(ArgusHeaderNames.Authorization, out var value) ? value : null;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Headers.Remove(ArgusHeaderNames.Authorization);
                    return;
                }

                EnsureNoLineBreaks(value, nameof(value));
                Headers[ArgusHeaderNames.Authorization] = value.Trim();
            }
        }

        /// <summary>
        /// Gets the authorization scheme parsed from the <c>Authorization</c> header (e.g. "Bearer"), or <c>null</c> when the
        /// header is absent. Schemes are case-insensitive, so compare them with <see cref="StringComparison.OrdinalIgnoreCase"/>.
        /// </summary>
        public string AuthorizationScheme => this.ParseAuthorization().Scheme;

        /// <summary>
        /// Gets the authorization parameter parsed from the <c>Authorization</c> header: everything after the scheme, trimmed
        /// (e.g. "token123"). <c>null</c> when the header is absent or has a scheme only.
        /// </summary>
        public string AuthorizationParameter => this.ParseAuthorization().Parameter;

        /// <summary>
        /// Sets the <c>Authorization</c> header to <c>{scheme} {parameter}</c>, or to <c>{scheme}</c> when
        /// <paramref name="parameter"/> is <c>null</c> or white space
        /// </summary>
        /// <param name="scheme">
        /// The authorization scheme, a single token such as "Bearer" or "ApiKey"
        /// </param>
        /// <param name="parameter">
        /// The credentials for the scheme, such as a token; optional
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="scheme"/> is <c>null</c>, white space or contains white space, or when either value
        /// contains a carriage return or line feed
        /// </exception>
        public void SetAuthorization(string scheme, string parameter = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scheme);

            if (scheme.Any(char.IsWhiteSpace))
            {
                throw new ArgumentException("The authorization scheme must be a single token without white space.", nameof(scheme));
            }

            if (parameter != null)
            {
                EnsureNoLineBreaks(parameter, nameof(parameter));
            }

            this.Authorization = string.IsNullOrWhiteSpace(parameter) ? scheme : scheme + " " + parameter.Trim();
        }

        /// <summary>
        /// Throws when a header value contains a carriage return or line feed
        /// </summary>
        /// <param name="value">
        /// The header value to check
        /// </param>
        /// <param name="parameterName">
        /// The name of the parameter that supplied the value
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="value"/> contains a carriage return or line feed
        /// </exception>
        private static void EnsureNoLineBreaks(string value, string parameterName)
        {
            if (value.AsSpan().IndexOfAny('\r', '\n') >= 0)
            {
                throw new ArgumentException("A header value must not contain carriage returns or line feeds.", parameterName);
            }
        }

        /// <summary>
        /// Splits the <c>Authorization</c> header into its scheme and parameter at the first white space
        /// </summary>
        /// <returns>
        /// The scheme and parameter; each is <c>null</c> when absent
        /// </returns>
        private (string Scheme, string Parameter) ParseAuthorization()
        {
            var value = this.Authorization?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                return (null, null);
            }

            var separator = value.AsSpan().IndexOfAny(' ', '\t');

            if (separator < 0)
            {
                return (value, null);
            }

            var parameter = value.Substring(separator + 1).Trim();

            return (value.Substring(0, separator), parameter.Length > 0 ? parameter : null);
        }
    }
}
