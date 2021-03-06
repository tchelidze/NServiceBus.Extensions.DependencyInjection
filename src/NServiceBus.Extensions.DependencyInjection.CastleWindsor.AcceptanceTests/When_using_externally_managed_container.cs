﻿namespace NServiceBus.Extensions.DependencyInjection.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Castle.MicroKernel.Registration;
    using Castle.Windsor;
    using Castle.Windsor.MsDependencyInjection;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_using_externally_managed_container : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_resolve_dependencies_from_serviceprovider()
        {
            IWindsorContainer container = null;
            IServiceProvider serviceProvider = null;

            var context = await Scenario.Define<Context>()
                .WithEndpoint<EndpointWithExternallyManagedContainer>(endpointBuilder =>
                {
                    var serviceCollection = new ServiceCollection();
                    // register service using the IServiceCollection API:
                    serviceCollection.AddSingleton<ServiceCollectionService>();

                    // register service using the NServiceBus container API:
                    endpointBuilder.CustomConfig(endpointConfiguration => endpointConfiguration
                        .RegisterComponents(c => c
                            .RegisterSingleton(new InternalApiService())));

                    endpointBuilder.ToCreateInstance(
                        configuration => Task.FromResult(EndpointWithExternallyManagedServiceProvider
                            .Create(configuration, serviceCollection)),
                        startableEndpoint =>
                        {
                            var windsorFactory = new WindsorServiceProviderFactory();
                            container = windsorFactory.CreateBuilder(serviceCollection);
                            // register service using the container native API:
                            container.Register(Component.For<NativeApiService>().LifeStyle.Singleton);

                            serviceProvider = windsorFactory.CreateServiceProvider(container);
                            return startableEndpoint.Start(serviceProvider);
                        });

                    endpointBuilder.When(session => session.SendLocal(new TestMessage()));
                })
                .Done(c => c.InjectedInternalApiService != null)
                .Run();

            Assert.AreSame(context.InjectedNativeApiService, container.Resolve<NativeApiService>());
            Assert.AreSame(context.InjectedNativeApiService, serviceProvider.GetService<NativeApiService>());
            Assert.AreSame(context.InjectedInternalApiService, container.Resolve<InternalApiService>());
            Assert.AreSame(context.InjectedInternalApiService, serviceProvider.GetService<InternalApiService>());
            Assert.AreSame(context.InjectedServiceCollectionService, container.Resolve<ServiceCollectionService>());
            Assert.AreSame(context.InjectedServiceCollectionService, serviceProvider.GetService<ServiceCollectionService>());
        }

        class Context : ScenarioContext
        {
            public InternalApiService InjectedInternalApiService { get; set; }
            public NativeApiService InjectedNativeApiService { get; set; }
            public ServiceCollectionService InjectedServiceCollectionService { get; set; }
        }

        class EndpointWithExternallyManagedContainer : EndpointConfigurationBuilder
        {
            public EndpointWithExternallyManagedContainer()
            {
                EndpointSetup<ExternallyManagedContainerServer>();
            }

            public class MessageHandler : IHandleMessages<TestMessage>
            {
                Context testContext;
                InternalApiService internalApiService;
                NativeApiService nativeApiService;
                ServiceCollectionService serviceCollectionService;

                public MessageHandler(Context testContext, InternalApiService internalApiService, NativeApiService nativeApiService, ServiceCollectionService serviceCollectionService)
                {
                    this.testContext = testContext;
                    this.internalApiService = internalApiService;
                    this.nativeApiService = nativeApiService;
                    this.serviceCollectionService = serviceCollectionService;
                }

                public Task Handle(TestMessage message, IMessageHandlerContext context)
                {
                    testContext.InjectedInternalApiService = internalApiService;
                    testContext.InjectedNativeApiService = nativeApiService;
                    testContext.InjectedServiceCollectionService = serviceCollectionService;
                    return Task.CompletedTask;
                }
            }
        }

        class TestMessage : ICommand
        {
        }

        class InternalApiService
        {
        }

        class NativeApiService
        {
        }

        class ServiceCollectionService
        {
        }
    }
}