// -------------------------------------------------------------------------------------------------
//   <copyright file="HealthServiceMessageCommandType.cs">
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

namespace ArgusTransfer.Protocol
{
    /// <summary>
    /// Enumeration of HTTP-like verbs used in the Argus IPC protocol
    /// </summary>
    public enum ArgusVerb
    {
        /// <summary>
        /// Retrieve a resource or collection of resources
        /// </summary>
        GET,

        /// <summary>
        /// Create a new resource
        /// </summary>
        POST,

        /// <summary>
        /// Replace an existing resource
        /// </summary>
        PUT,

        /// <summary>
        /// Partially update an existing resource
        /// </summary>
        PATCH,

        /// <summary>
        /// Retrieve metadata about a resource without the body
        /// </summary>
        HEAD,

        /// <summary>
        /// Delete an existing resource
        /// </summary>
        DELETE
    }
}
