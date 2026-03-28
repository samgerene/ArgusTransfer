// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusChunkedEncoding.cs">
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
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods for reading and writing HTTP/1.1-style chunked transfer encoding
    /// used by the ARGUS/1.0 protocol for streaming message bodies
    /// </summary>
    public static class ArgusChunkedEncoding
    {
        /// <summary>
        /// The default chunk size in bytes
        /// </summary>
        private const int DefaultChunkSize = 8192;

        /// <summary>
        /// Writes the contents of a <see cref="Stream"/> in chunked transfer encoding to a <see cref="StreamWriter"/>
        /// </summary>
        /// <param name="source">
        /// The <see cref="Stream"/> to read data from
        /// </param>
        /// <param name="writer">
        /// The <see cref="StreamWriter"/> to write chunked data to
        /// </param>
        /// <param name="chunkSize">
        /// The maximum number of bytes per chunk
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        public static async Task WriteChunkedAsync(Stream source, StreamWriter writer, int chunkSize = DefaultChunkSize, CancellationToken cancellationToken = default)
        {
            var buffer = new byte[chunkSize];
            int bytesRead;

            while ((bytesRead = await source.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
            {
                await writer.WriteAsync(bytesRead.ToString("x", CultureInfo.InvariantCulture));
                await writer.WriteAsync("\r\n");
                await writer.WriteAsync(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                await writer.WriteAsync("\r\n");
                await writer.FlushAsync(cancellationToken);
            }

            await writer.WriteAsync("0\r\n\r\n");
            await writer.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Reads chunked transfer encoded data from a <see cref="StreamReader"/> into a <see cref="MemoryStream"/>
        /// </summary>
        /// <param name="reader">
        /// The <see cref="StreamReader"/> to read chunked data from
        /// </param>
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes. A value of 0 disables the limit.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="MemoryStream"/> containing the de-chunked data, positioned at the beginning
        /// </returns>
        public static async Task<MemoryStream> ReadChunkedAsync(StreamReader reader, long maxBodySize = 0, CancellationToken cancellationToken = default)
        {
            var result = new MemoryStream();
            long totalBytesRead = 0;

            while (true)
            {
                var sizeLine = await reader.ReadLineAsync(cancellationToken);

                if (string.IsNullOrEmpty(sizeLine))
                {
                    break;
                }

                var chunkSize = int.Parse(sizeLine.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                if (chunkSize == 0)
                {
                    // Read the trailing empty line after the terminating chunk
                    await reader.ReadLineAsync(cancellationToken);
                    break;
                }

                totalBytesRead += chunkSize;

                if (maxBodySize > 0 && totalBytesRead > maxBodySize)
                {
                    throw new InvalidOperationException(
                        $"Chunked body size exceeds the maximum allowed size of {maxBodySize} bytes.");
                }

                var charBuffer = new char[chunkSize];
                var totalRead = 0;

                while (totalRead < chunkSize)
                {
                    var read = await reader.ReadAsync(charBuffer, totalRead, chunkSize - totalRead);

                    if (read == 0)
                    {
                        break;
                    }

                    totalRead += read;
                }

                var bytes = Encoding.UTF8.GetBytes(charBuffer, 0, totalRead);
                await result.WriteAsync(bytes.AsMemory(), cancellationToken);

                // Read the trailing \r\n after the chunk data
                await reader.ReadLineAsync(cancellationToken);
            }

            result.Position = 0;
            return result;
        }

        /// <summary>
        /// Writes the contents of a <see cref="Stream"/> in chunked transfer encoding to a <see cref="StringBuilder"/>
        /// </summary>
        /// <param name="source">
        /// The <see cref="Stream"/> to read data from
        /// </param>
        /// <param name="sb">
        /// The <see cref="StringBuilder"/> to append chunked data to
        /// </param>
        /// <param name="chunkSize">
        /// The maximum number of bytes per chunk
        /// </param>
        public static void WriteChunked(Stream source, StringBuilder sb, int chunkSize = DefaultChunkSize)
        {
            var buffer = new byte[chunkSize];
            int bytesRead;

            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                sb.Append(bytesRead.ToString("x", CultureInfo.InvariantCulture));
                sb.Append("\r\n");
                sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                sb.Append("\r\n");
            }

            sb.Append("0\r\n\r\n");
        }

        /// <summary>
        /// Reads chunked transfer encoded data from a <see cref="StringReader"/> into a <see cref="MemoryStream"/>
        /// </summary>
        /// <param name="reader">
        /// The <see cref="StringReader"/> to read chunked data from
        /// </param>
        /// <param name="maxBodySize">
        /// The maximum allowed body size in bytes. A value of 0 disables the limit.
        /// </param>
        /// <returns>
        /// A <see cref="MemoryStream"/> containing the de-chunked data, positioned at the beginning
        /// </returns>
        public static MemoryStream ReadChunked(StringReader reader, long maxBodySize = 0)
        {
            var result = new MemoryStream();
            long totalBytesRead = 0;

            while (true)
            {
                var sizeLine = reader.ReadLine();

                if (string.IsNullOrEmpty(sizeLine))
                {
                    break;
                }

                var chunkSize = int.Parse(sizeLine.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                if (chunkSize == 0)
                {
                    // Read the trailing empty line after the terminating chunk
                    reader.ReadLine();
                    break;
                }

                totalBytesRead += chunkSize;

                if (maxBodySize > 0 && totalBytesRead > maxBodySize)
                {
                    throw new InvalidOperationException(
                        $"Chunked body size exceeds the maximum allowed size of {maxBodySize} bytes.");
                }

                var charBuffer = new char[chunkSize];
                var totalRead = 0;

                while (totalRead < chunkSize)
                {
                    var read = reader.Read(charBuffer, totalRead, chunkSize - totalRead);

                    if (read == 0)
                    {
                        break;
                    }

                    totalRead += read;
                }

                var bytes = Encoding.UTF8.GetBytes(charBuffer, 0, totalRead);
                result.Write(bytes, 0, bytes.Length);

                // Read the trailing \r\n after the chunk data
                reader.ReadLine();
            }

            result.Position = 0;
            return result;
        }
    }
}
