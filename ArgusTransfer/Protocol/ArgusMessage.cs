// ------------------------------------------------------------------------------------------------
// <copyright file="ArgusMessage.cs">
//
//   Copyright (c) 2025-2026 Sam Gerené
//   SPDX-License-Identifier: Apache-2.0
//
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Protocol
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Abstract class from which all Argus messages derive
    /// </summary>
    public abstract class ArgusMessage
    {
        /// <summary>
        /// Backing field for <see cref="Body"/>
        /// </summary>
        private string body;

        /// <summary>
        /// Backing field for <see cref="BodyStream"/>
        /// </summary>
        private Stream bodyStream;

        /// <summary>
        /// A token that can be used to correlate messages
        /// </summary>
        public Guid CorrelationToken { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The <see cref="DateTime"/> the message was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the headers dictionary for additional metadata carried with the message
        /// </summary>
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The body of the message as a string, typically used to carry a text payload.
        /// Setting this property clears <see cref="BodyStream"/>.
        /// </summary>
        public string Body
        {
            get => this.body;
            set
            {
                this.body = value;
                this.bodyStream = null;
            }
        }

        /// <summary>
        /// The body of the message as a <see cref="Stream"/>, used to carry large or binary payloads
        /// via chunked transfer encoding. Setting this property clears <see cref="Body"/>.
        /// </summary>
        public Stream BodyStream
        {
            get => this.bodyStream;
            set
            {
                this.bodyStream = value;
                this.body = null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this message carries a streamed body
        /// </summary>
        public bool IsStreamed => this.bodyStream != null;
    }
}
