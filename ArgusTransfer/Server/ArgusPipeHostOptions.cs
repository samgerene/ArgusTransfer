// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusPipeHostOptions.cs">
//
//     Copyright (c) 2025-2026 Sam Gerené
//
//     Licensed under the Apache License, Version 2.0 (the "License");
//     you may not use this file except in compliance with the License.
//     You may obtain a copy of the License at
//
//         http://www.apache.org/licenses/LICENSE-2.0
//
//     Unless required by applicable law or agreed to in writing, software
//     distributed under the License is distributed on an "AS IS" BASIS,
//     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//     See the License for the specific language governing permissions and
//     limitations under the License.
//
//   </copyright>
//   ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Server
{
    using System;
    using System.IO.Pipes;
    using System.Threading;

    using ArgusTransfer.Serialization;

    /// <summary>
    /// Configuration options for the <see cref="ArgusPipeHostBackgroundService"/>. Every option validates the value it is
    /// given, so an invalid value fails where it is configured and not later, while a request is being handled.
    /// </summary>
    public class ArgusPipeHostOptions
    {
        /// <summary>
        /// The longest finite timeout the options accept, about 49.7 days: the longest duration the timers behind the
        /// timeouts support. Use <see cref="Timeout.InfiniteTimeSpan"/> for no timeout.
        /// </summary>
        internal static readonly TimeSpan MaxTimeout = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

        /// <summary>
        /// The shortest timeout the request timeouts accept, one millisecond: the timers behind the timeouts count whole
        /// milliseconds, so a shorter duration would expire immediately.
        /// </summary>
        internal static readonly TimeSpan MinTimeout = TimeSpan.FromMilliseconds(1);

        /// <summary>
        /// Backing field for <see cref="PipeName"/>
        /// </summary>
        private string pipeName = "argus";

        /// <summary>
        /// Backing field for <see cref="MaxRequestBodySize"/>
        /// </summary>
        private long maxRequestBodySize = 1_048_576;

        /// <summary>
        /// Backing field for <see cref="MaxRequestHeaderSize"/>
        /// </summary>
        private int maxRequestHeaderSize = 32 * 1024;

        /// <summary>
        /// Backing field for <see cref="ShutdownDrainTimeout"/>
        /// </summary>
        private TimeSpan shutdownDrainTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Backing field for <see cref="RequestTimeout"/>
        /// </summary>
        private TimeSpan requestTimeout = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Backing field for <see cref="MaxConcurrentRequests"/>
        /// </summary>
        private int maxConcurrentRequests = 10;

        /// <summary>
        /// Backing field for <see cref="RequestReadTimeout"/>
        /// </summary>
        private TimeSpan requestReadTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Backing field for <see cref="ResponseWriteTimeout"/>
        /// </summary>
        private TimeSpan responseWriteTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Backing field for <see cref="MaxConcurrentConnections"/>
        /// </summary>
        private int maxConcurrentConnections = 100;

        /// <summary>
        /// Gets or sets the name of the named pipe to listen on. Defaults to "argus"
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the value is <c>null</c>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the value is empty or consists only of white space
        /// </exception>
        public string PipeName
        {
            get => this.pipeName;
            set
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value), $"{nameof(this.PipeName)} must not be null.");
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"{nameof(this.PipeName)} must not be empty or consist only of white space.", nameof(value));
                }

                this.pipeName = value;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="PipeSecurity"/> applied to the named pipe on Windows. When
        /// <c>null</c>, the host applies a default ACL granting Authenticated Users
        /// <see cref="PipeAccessRights.ReadWrite"/> and <see cref="PipeAccessRights.Synchronize"/>,
        /// which lets a user-session client connect to a pipe owned by a service running as
        /// LocalSystem. Set this to a custom <see cref="PipeSecurity"/> to lock the pipe down
        /// further (e.g. to a specific group SID). Ignored on non-Windows platforms.
        /// </summary>
        public PipeSecurity PipeSecurity { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowed request body size in bytes. Defaults to 1 MB (1,048,576 bytes).
        /// Requests with a Content-Length exceeding this limit are rejected with a 400 Bad Request response.
        /// A value of 0 disables the limit.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is negative
        /// </exception>
        public long MaxRequestBodySize
        {
            get => this.maxRequestBodySize;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(this.MaxRequestBodySize)} must be 0 (no limit) or greater.");
                }

                this.maxRequestBodySize = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum size in bytes of a request's header block -- the request line plus all header lines.
        /// Defaults to 32 KB (32,768 bytes). The host stops reading as soon as the limit is exceeded and rejects the request
        /// with a 400 Bad Request response, so a client cannot exhaust memory with an endless header line or endless headers.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is zero or negative
        /// </exception>
        public int MaxRequestHeaderSize
        {
            get => this.maxRequestHeaderSize;
            set => this.maxRequestHeaderSize = EnsurePositive(value, nameof(this.MaxRequestHeaderSize));
        }

        /// <summary>
        /// Gets or sets the maximum time to wait for in-flight requests to complete during shutdown.
        /// Defaults to 30 seconds. After this timeout, remaining requests are cancelled. A value of
        /// <see cref="TimeSpan.Zero"/> cancels them immediately; <see cref="Timeout.InfiniteTimeSpan"/> waits until they
        /// have completed or the host's own shutdown timeout expires.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is negative and not <see cref="Timeout.InfiniteTimeSpan"/>, or greater than
        /// about 49.7 days
        /// </exception>
        public TimeSpan ShutdownDrainTimeout
        {
            get => this.shutdownDrainTimeout;
            set => this.shutdownDrainTimeout = EnsureTimeout(value, allowZero: true, nameof(this.ShutdownDrainTimeout));
        }

        /// <summary>
        /// Gets or sets the maximum time allowed for a single request handler to execute.
        /// Defaults to 60 seconds. Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable the timeout.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than one millisecond and not <see cref="Timeout.InfiniteTimeSpan"/>, or greater
        /// than about 49.7 days
        /// </exception>
        public TimeSpan RequestTimeout
        {
            get => this.requestTimeout;
            set => this.requestTimeout = EnsureTimeout(value, allowZero: false, nameof(this.RequestTimeout));
        }

        /// <summary>
        /// Gets or sets the maximum number of requests that can be processed concurrently.
        /// Defaults to 10. When the limit is reached, new requests receive a 503 Service Unavailable response.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is zero or negative
        /// </exception>
        public int MaxConcurrentRequests
        {
            get => this.maxConcurrentRequests;
            set => this.maxConcurrentRequests = EnsurePositive(value, nameof(this.MaxConcurrentRequests));
        }

        /// <summary>
        /// Gets or sets the maximum time a client may take to send its complete request -- request line, headers and
        /// body -- after connecting. Defaults to 30 seconds. When it expires, the host logs a warning and closes the
        /// connection, so idle or deliberately slow clients cannot hold a connection indefinitely. Set to
        /// <see cref="Timeout.InfiniteTimeSpan"/> to disable the timeout.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than one millisecond and not <see cref="Timeout.InfiniteTimeSpan"/>, or greater
        /// than about 49.7 days
        /// </exception>
        public TimeSpan RequestReadTimeout
        {
            get => this.requestReadTimeout;
            set => this.requestReadTimeout = EnsureTimeout(value, allowZero: false, nameof(this.RequestReadTimeout));
        }

        /// <summary>
        /// Gets or sets the maximum time the host may take to send a complete response -- status line, headers and
        /// body -- to the client. Defaults to 30 seconds. The pipe is unbuffered, so the host can only send as fast as the
        /// client reads; when the timeout expires, the host logs a warning and closes the connection, so a client that
        /// does not read its response, or reads it too slowly, cannot hold a connection indefinitely. The timeout is a
        /// budget for the whole response and not an idle timeout: for a streamed body it also counts the time the
        /// handler's stream takes to produce the data. Increase it, or disable it, on a host that streams large or
        /// long-lived responses. Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable the timeout.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than one millisecond and not <see cref="Timeout.InfiniteTimeSpan"/>, or greater
        /// than about 49.7 days
        /// </exception>
        public TimeSpan ResponseWriteTimeout
        {
            get => this.responseWriteTimeout;
            set => this.responseWriteTimeout = EnsureTimeout(value, allowZero: false, nameof(this.ResponseWriteTimeout));
        }

        /// <summary>
        /// Gets or sets the maximum number of simultaneous client connections, including connections whose request is still
        /// being received. Defaults to 100. When the limit is reached, the host does not read new connections until one is
        /// closed. Depending on the platform, further clients either wait in <c>ConnectAsync</c> or connect immediately
        /// (on Linux and macOS named pipes are Unix domain sockets, and the operating system queues the connection; on
        /// Windows one client can connect to the pipe instance the host keeps ready) and then wait for their request to be
        /// read. In both cases the client's own timeout applies. Together with
        /// <see cref="MaxRequestHeaderSize"/> and <see cref="MaxRequestBodySize"/>, this bounds the memory used for requests.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is zero or negative
        /// </exception>
        public int MaxConcurrentConnections
        {
            get => this.maxConcurrentConnections;
            set => this.maxConcurrentConnections = EnsurePositive(value, nameof(this.MaxConcurrentConnections));
        }

        /// <summary>
        /// Gets or sets the <see cref="ArgusCompressionOptions"/> for request and response bodies. Requests compressed with
        /// one of the configured encodings are always decoded; <see cref="MaxRequestBodySize"/> limits both the compressed
        /// and the decompressed size. When <see cref="ArgusCompressionOptions.Enabled"/> is <c>true</c>, responses whose body
        /// reaches <see cref="ArgusCompressionOptions.MinimumBodySize"/> are compressed if the request's
        /// <c>Accept-Encoding</c> header accepts one of the configured encodings. Disabled by default. When set to
        /// <c>null</c>, the host uses the default options.
        /// </summary>
        public ArgusCompressionOptions Compression { get; set; } = new ArgusCompressionOptions();

        /// <summary>
        /// Checks that the value of an option is greater than zero
        /// </summary>
        /// <param name="value">
        /// The value to check
        /// </param>
        /// <param name="option">
        /// The name of the option, for the exception message
        /// </param>
        /// <returns>
        /// The <paramref name="value"/>
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is zero or negative
        /// </exception>
        private static int EnsurePositive(int value, string option)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"{option} must be greater than zero.");
            }

            return value;
        }

        /// <summary>
        /// Checks that the value of a timeout option is a duration the host's timers accept: at least one millisecond (or
        /// zero when <paramref name="allowZero"/> is set) up to <see cref="MaxTimeout"/>, or
        /// <see cref="Timeout.InfiniteTimeSpan"/>. The timers count whole milliseconds, so a shorter positive duration
        /// would expire immediately, like zero.
        /// </summary>
        /// <param name="value">
        /// The value to check
        /// </param>
        /// <param name="allowZero">
        /// <c>true</c> when <see cref="TimeSpan.Zero"/> is a meaningful value for the option
        /// </param>
        /// <param name="option">
        /// The name of the option, for the exception message
        /// </param>
        /// <returns>
        /// The <paramref name="value"/>
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is not an accepted duration
        /// </exception>
        private static TimeSpan EnsureTimeout(TimeSpan value, bool allowZero, string option)
        {
            if (value == Timeout.InfiniteTimeSpan)
            {
                return value;
            }

            var tooSmall = allowZero ? value < TimeSpan.Zero : value < MinTimeout;

            if (tooSmall || value > MaxTimeout)
            {
                var lowerBound = allowZero ? "zero or greater" : "at least 1 millisecond";

                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"{option} must be {lowerBound} and at most {MaxTimeout}, or Timeout.InfiniteTimeSpan for no timeout.");
            }

            return value;
        }
    }
}
