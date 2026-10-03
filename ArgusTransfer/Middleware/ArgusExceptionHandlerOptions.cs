// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusExceptionHandlerOptions.cs">
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

namespace ArgusTransfer.Middleware
{
    /// <summary>
    /// Configuration options for <see cref="ArgusExceptionHandlerMiddleware"/>
    /// </summary>
    public class ArgusExceptionHandlerOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the exception message, type and stack trace are included
        /// in the problem details response. Defaults to <c>false</c>. Enable this only in development
        /// environments, because exception details can expose sensitive information to clients.
        /// </summary>
        public bool IncludeExceptionDetails { get; set; }
    }
}
