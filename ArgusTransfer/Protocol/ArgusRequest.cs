// -------------------------------------------------------------------------------------------------
//   <copyright file="HealthServiceMessage.cs" company="Sam Gerené">
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
    using System;

    /// <summary>
    /// The purpose of the <see cref="ArgusRequest"/> is to support a request-response
    /// protocol that uses HTTP like structure
    /// </summary>
    public class ArgusRequest : ArgusMessage
    {
        /// <summary>
        /// The verb used to determine the type of operation
        /// </summary>
        public ArgusVerb Verb { get; set; }

        /// <summary>
        /// The route path for the request (e.g. "/healthendpoint",
        /// "/healthendpoint/{identifier:Guid}")
        /// </summary>
        public string Route { get; set; }
    }
}
