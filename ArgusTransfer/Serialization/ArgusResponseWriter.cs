// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusResponseWriter.cs" company="Sam Gerené">
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
    /// Serializes an <see cref="ArgusResponse"/> to the ARGUS/1.0 text wire format
    /// </summary>
    public static class ArgusResponseWriter
    {
        /// <summary>
        /// Serializes an <see cref="ArgusResponse"/> to its text wire format representation
        /// </summary>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <returns>
        /// A string containing the serialized response in ARGUS/1.0 wire format
        /// </returns>
        public static string Write(ArgusResponse response)
        {
            var sb = new StringBuilder();

            sb.Append("ARGUS/1.0 ");
            sb.Append(((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(response.StatusCode.ToReasonPhrase());
            sb.Append("\r\n");

            sb.Append("X-Correlation-Token: ");
            sb.Append(response.CorrelationToken.ToString());
            sb.Append("\r\n");

            sb.Append("X-Timestamp: ");
            sb.Append(response.Timestamp.ToString("o", CultureInfo.InvariantCulture));
            sb.Append("\r\n");

            foreach (var header in response.Headers)
            {
                sb.Append(header.Key);
                sb.Append(": ");
                sb.Append(header.Value);
                sb.Append("\r\n");
            }

            if (!string.IsNullOrEmpty(response.Body))
            {
                var bodyBytes = Encoding.UTF8.GetByteCount(response.Body);

                if (!response.Headers.ContainsKey("Content-Type"))
                {
                    sb.Append("Content-Type: application/json\r\n");
                }

                sb.Append("Content-Length: ");
                sb.Append(bodyBytes.ToString(CultureInfo.InvariantCulture));
                sb.Append("\r\n");
            }

            sb.Append("\r\n");

            if (!string.IsNullOrEmpty(response.Body))
            {
                sb.Append(response.Body);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Writes an <see cref="ArgusResponse"/> in ARGUS/1.0 wire format to a <see cref="StreamWriter"/>
        /// </summary>
        /// <param name="writer">
        /// The <see cref="StreamWriter"/> to write to
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        public static void Write(StreamWriter writer, ArgusResponse response)
        {
            writer.Write(Write(response));
            writer.Flush();
        }
    }
}
