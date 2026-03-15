// -------------------------------------------------------------------------------------------------
//   <copyright file="IArgusMiddleware.cs">
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

namespace ArgusTransfer.Routing
{
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a middleware component that can inspect, modify, or short-circuit
    /// an Argus request as it flows through the pipeline
    /// </summary>
    public interface IArgusMiddleware
    {
        /// <summary>
        /// Processes an Argus request. Implementations should call <paramref name="next"/>
        /// to pass control to the next middleware in the pipeline, or skip it to short-circuit
        /// </summary>
        /// <param name="context">
        /// The <see cref="ArgusContext"/> for the current request
        /// </param>
        /// <param name="next">
        /// The <see cref="ArgusRequestDelegate"/> representing the next middleware in the pipeline
        /// </param>
        /// <returns>
        /// An awaitable <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        Task InvokeAsync(ArgusContext context, ArgusRequestDelegate next);
    }
}
