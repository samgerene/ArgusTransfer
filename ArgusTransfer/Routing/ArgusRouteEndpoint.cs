// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRouteEndpoint.cs" >
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

namespace ArgusTransfer.Routing
{
    using System.Collections.Generic;

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Represents a registered route endpoint with its verb, route template,
    /// handler delegate, and optional metadata
    /// </summary>
    internal class ArgusRouteEndpoint
    {
        /// <summary>
        /// Gets or sets the <see cref="ArgusVerb"/> this endpoint responds to
        /// </summary>
        public ArgusVerb Verb { get; set; }

        /// <summary>
        /// Gets or sets the route template for this endpoint
        /// (e.g. "/healthendpoint/{identifier:Guid}")
        /// </summary>
        public string RouteTemplate { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the <see cref="ArgusHandlerDelegate"/> that handles matching requests
        /// </summary>
        public ArgusHandlerDelegate Handler { get; set; } = null!;

        /// <summary>
        /// Gets the metadata dictionary attached to this endpoint
        /// </summary>
        public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
    }
}
