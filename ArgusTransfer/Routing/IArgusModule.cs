// -------------------------------------------------------------------------------------------------
//   <copyright file="IArgusModule.cs">
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
    /// Contract for modules that register route handlers with the Argus IPC router
    /// </summary>
    public interface IArgusModule
    {
        /// <summary>
        /// Registers route handlers on the provided <see cref="IArgusRouteBuilder"/>
        /// </summary>
        /// <param name="app">
        /// The <see cref="IArgusRouteBuilder"/> to register routes on
        /// </param>
        void AddRoutes(IArgusRouteBuilder app);
    }
}
