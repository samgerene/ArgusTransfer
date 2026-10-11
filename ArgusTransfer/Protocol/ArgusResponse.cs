// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusResponse.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

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
