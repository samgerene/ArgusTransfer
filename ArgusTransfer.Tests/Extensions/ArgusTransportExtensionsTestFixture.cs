// -------------------------------------------------------------------------------------------------
//   <copyright file="ArgusTransportExtensionsTestFixture.cs" company="Sam Gerene">
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

namespace ArgusTransfer.Tests.Extensions
{
    using System.Linq;

    using ArgusTransfer.Extensions;
    using ArgusTransfer.Server;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests for the <see cref="ArgusTransportExtensions"/> class
    /// </summary>
    [TestFixture]
    public class ArgusTransportExtensionsTestFixture
    {
        [Test]
        public void Verify_that_AddArgusPipeHost_registers_hosted_service()
        {
            var services = new ServiceCollection();

            services.AddArgusPipeHost();

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IHostedService));

            Assert.That(descriptor, Is.Not.Null);
            Assert.That(descriptor.ImplementationType, Is.EqualTo(typeof(ArgusPipeHostBackgroundService)));
        }

        [Test]
        public void Verify_that_AddArgusPipeHost_with_configure_sets_pipe_name()
        {
            var services = new ServiceCollection();

            services.AddArgusPipeHost(options => options.PipeName = "custom-pipe");

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IConfigureOptions<ArgusPipeHostOptions>));

            Assert.That(descriptor, Is.Not.Null);
        }

        [Test]
        public void Verify_that_AddArgusPipeHost_without_configure_uses_default_pipe_name()
        {
            var services = new ServiceCollection();

            services.AddArgusPipeHost();

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IConfigureOptions<ArgusPipeHostOptions>));

            Assert.That(descriptor, Is.Null);
        }
    }
}
