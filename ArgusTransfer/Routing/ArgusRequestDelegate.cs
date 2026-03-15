// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRequestDelegate.cs">
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
    /// A function that can process an Argus request within the middleware pipeline
    /// </summary>
    /// <param name="context">
    /// The <see cref="ArgusContext"/> for the current request
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/> representing the asynchronous operation
    /// </returns>
    public delegate Task ArgusRequestDelegate(ArgusContext context);
}
