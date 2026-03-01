// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusRequestWriter.cs" company="Sam Gerené">
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

namespace ArgusTransfer.Serialization
{
    using System.Globalization;
    using System.IO;
    using System.Text;

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Serializes an <see cref="ArgusRequest"/> to the ARGUS/1.0 text wire format
    /// </summary>
    public static class ArgusRequestWriter
    {
        /// <summary>
        /// Serializes an <see cref="ArgusRequest"/> to its text wire format representation
        /// </summary>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> to serialize
        /// </param>
        /// <returns>
        /// A string containing the serialized request in ARGUS/1.0 wire format
        /// </returns>
        public static string Write(ArgusRequest request)
        {
            var sb = new StringBuilder();

            sb.Append(request.Verb.ToString());
            sb.Append(' ');
            sb.Append(request.Route);
            sb.Append(" ARGUS/1.0\r\n");

            sb.Append("X-Correlation-Token: ");
            sb.Append(request.CorrelationToken.ToString());
            sb.Append("\r\n");

            sb.Append("X-Timestamp: ");
            sb.Append(request.Timestamp.ToString("o", CultureInfo.InvariantCulture));
            sb.Append("\r\n");

            foreach (var header in request.Headers)
            {
                sb.Append(header.Key);
                sb.Append(": ");
                sb.Append(header.Value);
                sb.Append("\r\n");
            }

            if (!string.IsNullOrEmpty(request.Body))
            {
                var bodyBytes = Encoding.UTF8.GetByteCount(request.Body);

                if (!request.Headers.ContainsKey("Content-Type"))
                {
                    sb.Append("Content-Type: application/json\r\n");
                }

                sb.Append("Content-Length: ");
                sb.Append(bodyBytes.ToString(CultureInfo.InvariantCulture));
                sb.Append("\r\n");
            }

            sb.Append("\r\n");

            if (!string.IsNullOrEmpty(request.Body))
            {
                sb.Append(request.Body);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Writes an <see cref="ArgusRequest"/> in ARGUS/1.0 wire format to a <see cref="StreamWriter"/>
        /// </summary>
        /// <param name="writer">
        /// The <see cref="StreamWriter"/> to write to
        /// </param>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> to serialize
        /// </param>
        public static void Write(StreamWriter writer, ArgusRequest request)
        {
            writer.Write(Write(request));
            writer.Flush();
        }
    }
}
