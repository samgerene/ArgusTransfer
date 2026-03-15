// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusEndpointConventionBuilder.cs">
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
    /// <summary>
    /// Writes metadata to an <see cref="ArgusRouteEndpoint"/>
    /// </summary>
    internal class ArgusEndpointConventionBuilder : IArgusEndpointConventionBuilder
    {
        /// <summary>
        /// The <see cref="ArgusRouteEndpoint"/> being configured
        /// </summary>
        private readonly ArgusRouteEndpoint endpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusEndpointConventionBuilder"/> class
        /// </summary>
        /// <param name="endpoint">
        /// The <see cref="ArgusRouteEndpoint"/> to attach metadata to
        /// </param>
        public ArgusEndpointConventionBuilder(ArgusRouteEndpoint endpoint)
        {
            this.endpoint = endpoint;
        }

        /// <summary>
        /// Attaches a key-value metadata pair to the endpoint
        /// </summary>
        /// <param name="key">
        /// The metadata key
        /// </param>
        /// <param name="value">
        /// The metadata value
        /// </param>
        /// <returns>
        /// This <see cref="IArgusEndpointConventionBuilder"/> for method chaining
        /// </returns>
        public IArgusEndpointConventionBuilder WithMetadata(string key, string value)
        {
            this.endpoint.Metadata[key] = value;
            return this;
        }
    }
}
