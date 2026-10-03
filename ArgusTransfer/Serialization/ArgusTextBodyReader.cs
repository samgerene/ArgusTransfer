// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusTextBodyReader.cs">
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
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Reads a <c>Content-Length</c> delimited body from a <see cref="TextReader"/>, where <c>Content-Length</c> is the
    /// number of UTF-8 bytes rather than the number of characters
    /// </summary>
    internal static class ArgusTextBodyReader
    {
        /// <summary>
        /// The maximum number of characters requested from the reader at once
        /// </summary>
        private const int BufferSize = 4096;

        /// <summary>
        /// The maximum number of UTF-8 bytes a single UTF-16 character encodes to
        /// </summary>
        private const int MaxBytesPerChar = 3;

        /// <summary>
        /// Calculates how many characters can be requested from the reader without reading past the end of the body.
        /// A UTF-16 character encodes to at most three UTF-8 bytes, so requesting a third of the remaining bytes never
        /// overshoots for well-formed input; at least one character is requested so reading always progresses.
        /// </summary>
        /// <param name="bufferLength">
        /// The length of the character buffer
        /// </param>
        /// <param name="remainingBytes">
        /// The number of body bytes that have not been read yet
        /// </param>
        /// <returns>
        /// The number of characters to request
        /// </returns>
        private static int CharsToRequest(int bufferLength, int remainingBytes)
        {
            return Math.Max(1, Math.Min(bufferLength, remainingBytes / MaxBytesPerChar));
        }

        /// <summary>
        /// Reads characters until their UTF-8 encoding reaches <paramref name="byteCount"/> bytes or the reader ends
        /// </summary>
        /// <param name="reader">
        /// The <see cref="TextReader"/> positioned at the start of the body
        /// </param>
        /// <param name="byteCount">
        /// The body length in UTF-8 bytes
        /// </param>
        /// <returns>
        /// The body text; shorter than <paramref name="byteCount"/> bytes when the reader ends early
        /// </returns>
        public static string Read(TextReader reader, int byteCount)
        {
            var encoder = Encoding.UTF8.GetEncoder();
            var buffer = new char[Math.Min(byteCount, BufferSize)];
            var body = new StringBuilder();
            var bytesRead = 0;

            while (bytesRead < byteCount)
            {
                var read = reader.Read(buffer, 0, CharsToRequest(buffer.Length, byteCount - bytesRead));

                if (read == 0)
                {
                    break;
                }

                bytesRead += encoder.GetByteCount(buffer, 0, read, flush: false);
                body.Append(buffer, 0, read);
            }

            return body.ToString();
        }

        /// <summary>
        /// Asynchronously reads characters until their UTF-8 encoding reaches <paramref name="byteCount"/> bytes or the reader ends
        /// </summary>
        /// <param name="reader">
        /// The <see cref="TextReader"/> positioned at the start of the body
        /// </param>
        /// <param name="byteCount">
        /// The body length in UTF-8 bytes
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The body text; shorter than <paramref name="byteCount"/> bytes when the reader ends early
        /// </returns>
        public static async Task<string> ReadAsync(TextReader reader, int byteCount, CancellationToken cancellationToken)
        {
            var encoder = Encoding.UTF8.GetEncoder();
            var buffer = new char[Math.Min(byteCount, BufferSize)];
            var body = new StringBuilder();
            var bytesRead = 0;

            while (bytesRead < byteCount)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(0, CharsToRequest(buffer.Length, byteCount - bytesRead)), cancellationToken);

                if (read == 0)
                {
                    break;
                }

                bytesRead += encoder.GetByteCount(buffer, 0, read, flush: false);
                body.Append(buffer, 0, read);
            }

            return body.ToString();
        }
    }
}
