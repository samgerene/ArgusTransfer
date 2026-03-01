// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusHandlerDelegate.cs" company="Sam Gerené">
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
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Delegate that handles an <see cref="ArgusRequest"/> and returns an <see cref="ArgusResponse"/>
    /// </summary>
    /// <param name="request">
    /// The incoming <see cref="ArgusRequest"/>
    /// </param>
    /// <param name="routeValues">
    /// A dictionary of route parameter values extracted from the request route
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task{ArgusResponse}"/>
    /// </returns>
    public delegate Task<ArgusResponse> ArgusHandlerDelegate(ArgusRequest request, IReadOnlyDictionary<string, string> routeValues);
}
