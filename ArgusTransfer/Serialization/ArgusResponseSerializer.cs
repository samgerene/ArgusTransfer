// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusTextResponseSerializer.cs">
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

    using ArgusTransfer.Protocol;

    /// <summary>
    /// Serializes and deserializes <see cref="ArgusResponse"/> messages using the ARGUS/1.0 text wire format
    /// </summary>
    public class ArgusResponseSerializer
    {
        /// <summary>
        /// The <see cref="IArgusBodySerializer"/> used to serialize and deserialize the message body
        /// </summary>
        private readonly IArgusBodySerializer bodySerializer;

        /// <summary>
        /// The <see cref="IArgusBodySerializerRegistry"/> used to resolve serializers by content type
        /// </summary>
        private readonly IArgusBodySerializerRegistry bodySerializerRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusResponseSerializer"/> class
        /// with the default <see cref="PlainTextArgusBodySerializer"/>
        /// </summary>
        public ArgusResponseSerializer() : this(new PlainTextArgusBodySerializer())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusResponseSerializer"/> class
        /// </summary>
        /// <param name="bodySerializer">
        /// The <see cref="IArgusBodySerializer"/> used to serialize and deserialize the message body
        /// </param>
        public ArgusResponseSerializer(IArgusBodySerializer bodySerializer)
        {
            this.bodySerializer = bodySerializer;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusResponseSerializer"/> class
        /// with an <see cref="IArgusBodySerializerRegistry"/> for content-type based serializer resolution
        /// </summary>
        /// <param name="bodySerializerRegistry">
        /// The <see cref="IArgusBodySerializerRegistry"/> used to resolve serializers by content type
        /// </param>
        public ArgusResponseSerializer(IArgusBodySerializerRegistry bodySerializerRegistry)
            : this(bodySerializerRegistry.DefaultSerializer)
        {
            this.bodySerializerRegistry = bodySerializerRegistry;
        }

        /// <summary>
        /// Serializes an <see cref="ArgusResponse"/> to its text wire format representation
        /// </summary>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <returns>
        /// A string containing the serialized response in ARGUS/1.0 wire format
        /// </returns>
        public string Write(ArgusResponse response)
        {
            return WriteCore(response, this.ResolveSerializer(ArgusWireFormat.GetContentType(response)));
        }

        /// <summary>
        /// Serializes an <see cref="ArgusResponse"/> to its text wire format representation
        /// using a serializer resolved from the specified accept content type
        /// </summary>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="acceptContentType">
        /// The accept content type used to resolve the appropriate <see cref="IArgusBodySerializer"/>
        /// </param>
        /// <returns>
        /// A string containing the serialized response in ARGUS/1.0 wire format
        /// </returns>
        public string Write(ArgusResponse response, string acceptContentType)
        {
            var resolvedSerializer = this.ResolveSerializer(acceptContentType);

            if (!response.Headers.ContainsKey(ArgusHeaderNames.ContentType))
            {
                response.Headers[ArgusHeaderNames.ContentType] = resolvedSerializer.ContentType;
            }

            return WriteCore(response, resolvedSerializer);
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
        public void Write(StreamWriter writer, ArgusResponse response)
        {
            writer.Write(this.Write(response));
            writer.Flush();
        }

        /// <summary>
        /// Writes an <see cref="ArgusResponse"/> in ARGUS/1.0 wire format to a <see cref="StreamWriter"/>
        /// using a serializer resolved from the specified accept content type
        /// </summary>
        /// <param name="writer">
        /// The <see cref="StreamWriter"/> to write to
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="acceptContentType">
        /// The accept content type used to resolve the appropriate <see cref="IArgusBodySerializer"/>
        /// </param>
        public void Write(StreamWriter writer, ArgusResponse response, string acceptContentType)
        {
            writer.Write(this.Write(response, acceptContentType));
            writer.Flush();
        }

        /// <summary>
        /// Asynchronously writes an <see cref="ArgusResponse"/> in ARGUS/1.0 wire format to a <see cref="StreamWriter"/>.
        /// This method supports streaming bodies via chunked transfer encoding. The writer is flushed and the message is
        /// written as bytes to its <see cref="StreamWriter.BaseStream"/>, so binary streamed bodies are preserved.
        /// </summary>
        /// <param name="writer">
        /// The <see cref="StreamWriter"/> to write to
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        public async Task WriteAsync(StreamWriter writer, ArgusResponse response, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writer);

            await writer.FlushAsync(cancellationToken);
            await this.WriteAsync(writer.BaseStream, response, cancellationToken);
        }

        /// <summary>
        /// Asynchronously writes an <see cref="ArgusResponse"/> in ARGUS/1.0 wire format to a <see cref="StreamWriter"/>
        /// using a serializer resolved from the specified accept content type. The writer is flushed and the message is
        /// written as bytes to its <see cref="StreamWriter.BaseStream"/>, so binary streamed bodies are preserved.
        /// </summary>
        /// <param name="writer">
        /// The <see cref="StreamWriter"/> to write to
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="acceptContentType">
        /// The accept content type used to resolve the appropriate <see cref="IArgusBodySerializer"/>
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        public async Task WriteAsync(StreamWriter writer, ArgusResponse response, string acceptContentType, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writer);

            await writer.FlushAsync(cancellationToken);
            await this.WriteAsync(writer.BaseStream, response, acceptContentType, cancellationToken);
        }

        /// <summary>
        /// Asynchronously writes an <see cref="ArgusResponse"/> in ARGUS/1.0 wire format to a <see cref="Stream"/>.
        /// The header block and a string body are encoded as UTF-8; a streamed body is copied as raw bytes using
        /// chunked transfer encoding.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> to write to
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> or <paramref name="response"/> is <c>null</c>
        /// </exception>
        public Task WriteAsync(Stream stream, ArgusResponse response, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(response);

            return ArgusWireFormat.WriteAsync(stream, response, () => this.Write(response), () => BuildStreamedHead(response), cancellationToken);
        }

        /// <summary>
        /// Asynchronously writes an <see cref="ArgusResponse"/> in ARGUS/1.0 wire format to a <see cref="Stream"/>
        /// using a serializer resolved from the specified accept content type. The header block and a string body are
        /// encoded as UTF-8; a streamed body is copied as raw bytes using chunked transfer encoding.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> to write to
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="acceptContentType">
        /// The accept content type used to resolve the appropriate <see cref="IArgusBodySerializer"/>
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> or <paramref name="response"/> is <c>null</c>
        /// </exception>
        public Task WriteAsync(Stream stream, ArgusResponse response, string acceptContentType, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(response);

            return ArgusWireFormat.WriteAsync(stream, response, () => this.Write(response, acceptContentType), () => BuildStreamedHead(response), cancellationToken);
        }

        /// <summary>
        /// Deserializes an <see cref="ArgusResponse"/> from its text wire format representation
        /// </summary>
        /// <param name="text">
        /// The text containing the serialized response in ARGUS/1.0 wire format
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusResponse"/>
        /// </returns>
        public ArgusResponse Read(string text)
        {
            using var reader = new StringReader(text);

            var response = ParseStatusLine(reader.ReadLine());
            ArgusWireFormat.ReadHeadersAndBody(reader, response, 0, this.ResolveSerializer);

            return response;
        }

        /// <summary>
        /// Asynchronously deserializes an <see cref="ArgusResponse"/> from a <see cref="StreamReader"/>
        /// </summary>
        /// <param name="reader">
        /// The <see cref="StreamReader"/> to read from
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusResponse"/>
        /// </returns>
        /// <exception cref="EndOfStreamException">
        /// Thrown when the stream ends before a status line is received, for example because the connection was closed
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when the status line is empty or malformed
        /// </exception>
        public Task<ArgusResponse> ReadAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            return this.ReadCoreAsync(new ArgusTextMessageSource(reader), cancellationToken);
        }

        /// <summary>
        /// Asynchronously deserializes an <see cref="ArgusResponse"/> from a <see cref="Stream"/>. <c>Content-Length</c>
        /// and chunk sizes are honored as byte counts and a streamed body is read as raw bytes, so binary bodies are preserved.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> to read from. The method reads one message and may buffer bytes that belong to it only
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusResponse"/>
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> is <c>null</c>
        /// </exception>
        /// <exception cref="EndOfStreamException">
        /// Thrown when the stream ends before a status line or the complete body was received, for example because the connection was closed
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when the status line is empty or malformed, or a chunk is malformed
        /// </exception>
        public Task<ArgusResponse> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            return this.ReadCoreAsync(new ArgusWireReader(stream), cancellationToken);
        }

        /// <summary>
        /// Appends the status line (e.g. "ARGUS/1.0 200 OK")
        /// </summary>
        /// <param name="sb">
        /// The <see cref="StringBuilder"/> to append to
        /// </param>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> whose status code is written
        /// </param>
        private static void AppendStatusLine(StringBuilder sb, ArgusResponse response)
        {
            sb.Append("ARGUS/1.0 ");
            sb.Append(((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(response.StatusCode.ToReasonPhrase());
            sb.Append("\r\n");
        }

        /// <summary>
        /// Core write logic that serializes an <see cref="ArgusResponse"/> using the specified serializer
        /// </summary>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> to serialize
        /// </param>
        /// <param name="serializer">
        /// The <see cref="IArgusBodySerializer"/> used for a string body
        /// </param>
        /// <returns>
        /// The response in ARGUS/1.0 wire format
        /// </returns>
        private static string WriteCore(ArgusResponse response, IArgusBodySerializer serializer)
        {
            var sb = new StringBuilder();

            AppendStatusLine(sb, response);
            ArgusWireFormat.AppendStandardHeaders(sb, response);
            ArgusWireFormat.AppendBody(sb, response, serializer);

            return sb.ToString();
        }

        /// <summary>
        /// Builds the status line and headers of a response with a streamed body, including the
        /// <c>Transfer-Encoding: chunked</c> header and the empty line that ends the header block
        /// </summary>
        /// <param name="response">
        /// The <see cref="ArgusResponse"/> with a streamed body
        /// </param>
        /// <returns>
        /// The header block in ARGUS/1.0 wire format
        /// </returns>
        private static string BuildStreamedHead(ArgusResponse response)
        {
            var sb = new StringBuilder();

            AppendStatusLine(sb, response);
            ArgusWireFormat.AppendStandardHeaders(sb, response);
            ArgusWireFormat.AppendStreamedHeaderTail(sb, response);

            return sb.ToString();
        }

        /// <summary>
        /// Parses the status line (e.g. "ARGUS/1.0 200 OK") into an <see cref="ArgusResponse"/>
        /// </summary>
        /// <param name="statusLine">
        /// The status line, or <c>null</c> when the input ended before it
        /// </param>
        /// <returns>
        /// A new <see cref="ArgusResponse"/> with the status code set
        /// </returns>
        /// <exception cref="FormatException">
        /// Thrown when the status line is missing or malformed, or the status code is unknown
        /// </exception>
        private static ArgusResponse ParseStatusLine(string statusLine)
        {
            if (string.IsNullOrWhiteSpace(statusLine))
            {
                throw new FormatException("Missing status line.");
            }

            var firstSpace = statusLine.IndexOf(' ');

            if (firstSpace < 0)
            {
                throw new FormatException($"Invalid status line: {statusLine}");
            }

            var afterVersion = statusLine.Substring(firstSpace + 1);
            var secondSpace = afterVersion.IndexOf(' ');

            if (secondSpace < 0)
            {
                throw new FormatException($"Invalid status line: {statusLine}");
            }

            var codeStr = afterVersion.Substring(0, secondSpace);

            if (!int.TryParse(codeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                throw new FormatException($"Invalid status code: {codeStr}");
            }

            if (!ArgusStatusCodeExtensions.TryParse(code, out var statusCode))
            {
                throw new FormatException($"Unknown status code: {code}");
            }

            return new ArgusResponse
            {
                StatusCode = statusCode
            };
        }

        /// <summary>
        /// Reads a complete response from an <see cref="IArgusMessageSource"/>
        /// </summary>
        /// <param name="source">
        /// The <see cref="IArgusMessageSource"/> to read from
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The deserialized <see cref="ArgusResponse"/>
        /// </returns>
        /// <exception cref="EndOfStreamException">
        /// Thrown when the input ends before a status line is received
        /// </exception>
        private async Task<ArgusResponse> ReadCoreAsync(IArgusMessageSource source, CancellationToken cancellationToken)
        {
            var statusLine = await source.ReadLineAsync(cancellationToken)
                ?? throw new EndOfStreamException("The stream ended before a status line was received.");

            var response = ParseStatusLine(statusLine);
            await ArgusWireFormat.ReadHeadersAndBodyAsync(source, response, 0, this.ResolveSerializer, cancellationToken);

            return response;
        }

        /// <summary>
        /// Resolves the appropriate <see cref="IArgusBodySerializer"/> for the specified content type
        /// </summary>
        /// <param name="contentType">
        /// The content type to resolve, or <c>null</c>
        /// </param>
        /// <returns>
        /// The serializer registered for <paramref name="contentType"/>, or the default serializer
        /// </returns>
        private IArgusBodySerializer ResolveSerializer(string contentType)
        {
            return ArgusWireFormat.ResolveSerializer(this.bodySerializerRegistry, this.bodySerializer, contentType);
        }
    }
}
