// -------------------------------------------------------------------------------------------------
//   <copyright file="Program.cs" company="Sam Gerene">
//
//     Copyright (c) 2026 Sam Gerene
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

namespace ArgusTransfer.Sample
{
    using ArgusTransfer.Extensions;
    using ArgusTransfer.Routing;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Entry point for the ArgusTransfer sample application
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point that configures and runs the sample host
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        public static void Main(string[] args)
        {
            Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddArgusModules();
                    services.AddArgusPipeHost(o => o.PipeName = "sample");
                    services.AddHostedService<SampleClientService>();
                })
                .Build()
                .Run();
        }
    }
}
