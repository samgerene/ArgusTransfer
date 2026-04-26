// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusResponse.cs">
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

namespace ArgusTransfer.Protocol
{
    /// <summary>
    /// Represents a response message in the Argus IPC protocol
    /// </summary>
    public class ArgusResponse : ArgusMessage
    {
        /// <summary>
        /// Gets or sets the <see cref="ArgusStatusCode"/> indicating the result of the request
        /// </summary>
        public ArgusStatusCode StatusCode { get; set; }

        /// <summary>
        /// Returns the current <see cref="ArgusResponse"/> if <see cref="StatusCode"/> is in the 2xx range,
        /// otherwise throws an <see cref="ArgusRequestException"/> populated with the response details
        /// </summary>
        /// <returns>
        /// The current <see cref="ArgusResponse"/> instance, to allow fluent chaining
        /// </returns>
        /// <exception cref="ArgusRequestException">
        /// Thrown when <see cref="StatusCode"/> indicates a non-success result
        /// </exception>
        public ArgusResponse EnsureSuccessStatusCode()
        {
            var code = (int)this.StatusCode;

            if (code >= 200 && code < 300)
            {
                return this;
            }

            throw new ArgusRequestException(
                this.StatusCode,
                this.StatusCode.ToReasonPhrase(),
                this.Body);
        }
    }
}
