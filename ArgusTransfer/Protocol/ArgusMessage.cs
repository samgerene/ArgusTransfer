// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusMessage.cs" company="Sam Gerené">
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

namespace ArgusTransfer.Protocol
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Abstract class from which all Argus messages derive
    /// </summary>
    public abstract class ArgusMessage
    {
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
        /// The body of the message, typically used to carry a payload
        /// </summary>
        public string Body { get; set; }
    }
}
