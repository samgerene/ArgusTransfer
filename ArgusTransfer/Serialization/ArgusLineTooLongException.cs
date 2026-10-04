// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusLineTooLongException.cs">
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

namespace ArgusTransfer.Serialization
{
    using ArgusTransfer.Protocol;

    /// <summary>
    /// Thrown by an <see cref="IArgusMessageSource"/> when a line is longer than the length the caller allows. Callers that
    /// enforce a budget across several lines (the header block) translate it into a more specific <see cref="ArgusProtocolException"/>.
    /// </summary>
    internal sealed class ArgusLineTooLongException : ArgusProtocolException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusLineTooLongException"/> class
        /// </summary>
        /// <param name="maxLength">
        /// The maximum allowed line length in bytes
        /// </param>
        public ArgusLineTooLongException(int maxLength)
            : base($"A line exceeds the maximum allowed length of {maxLength} bytes.")
        {
        }
    }
}
