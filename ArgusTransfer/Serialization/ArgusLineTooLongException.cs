// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusLineTooLongException.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

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
