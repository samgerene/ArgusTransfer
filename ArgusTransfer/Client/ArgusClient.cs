// -------------------------------------------------------------------------------------------------
//  <copyright file="ArgusClient.cs" company="Sam Gerené">
//
//    Copyright (C) 2025 Sam Gerené
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, softwareUseCases
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Client
{
    using System;
    using System.IO;
    using System.IO.Pipes;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using ArgusTransfer.Protocol;
    using ArgusTransfer.Serialization;

    /// <summary>
    /// A low-level client that sends an <see cref="ArgusRequest"/> and receives an <see cref="ArgusResponse"/>
    /// over a named pipe using the ARGUS/1.0 wire protocol
    /// </summary>
    public class ArgusClient : IDisposable
    {
        /// <summary>
        /// The name of the named pipe to connect to
        /// </summary>
        private readonly string pipeName;

        /// <summary>
        /// Tracks whether this instance has been disposed
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusClient"/> class
        /// </summary>
        /// <param name="pipeName">
        /// The name of the named pipe to connect to. Defaults to "argus"
        /// </param>
        public ArgusClient(string pipeName = "argus")
        {
            this.pipeName = pipeName;
        }

        /// <summary>
        /// Sends an <see cref="ArgusRequest"/> over the named pipe and returns the <see cref="ArgusResponse"/>
        /// </summary>
        /// <param name="request">
        /// The <see cref="ArgusRequest"/> to send
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> used to signal cancellation
        /// </param>
        /// <returns>
        /// The <see cref="ArgusResponse"/> received from the server
        /// </returns>
        public async Task<ArgusResponse> SendAsync(ArgusRequest request, CancellationToken cancellationToken = default)
        {
            var pipeClient = new NamedPipeClientStream(".", this.pipeName, PipeDirection.InOut);

            try
            {
                await pipeClient.ConnectAsync(cancellationToken);

                var writer = new StreamWriter(pipeClient, new UTF8Encoding(false)) { AutoFlush = false };
                var reader = new StreamReader(pipeClient, new UTF8Encoding(false));

                ArgusRequestWriter.Write(writer, request);

                var response = await ArgusResponseReader.ReadAsync(reader, cancellationToken);

                return response;
            }
            finally
            {
                await pipeClient.DisposeAsync();
            }
        }

        /// <summary>
        /// Disposes the <see cref="ArgusClient"/>
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and optionally managed resources
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                this.disposed = true;
            }
        }
    }
}
