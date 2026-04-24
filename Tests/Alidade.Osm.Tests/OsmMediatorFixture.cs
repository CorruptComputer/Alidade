using Alidade.Core.PipelineBehaviors;
using Alidade.Osm.Handlers.Editing;
using Autofac;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Questy;
using Questy.Autofac;
using Questy.Autofac.Builder;

namespace Alidade.Osm.Tests;

/// <summary>
///   Builds a shared Autofac container with the real Questy pipeline wired up for all
///   Alidade.Osm handlers. Tests substitute external dependencies at scope level using
///   <c>BeginLifetimeScope</c>.
/// </summary>
public sealed class OsmMediatorFixture : IDisposable
{
    public IContainer Container { get; }

    public OsmMediatorFixture()
    {
        ContainerBuilder builder = new();

        QuestyConfigurationBuilder questyConfig = QuestyConfigurationBuilder
            .Create(typeof(FetchBbox).Assembly)
            .WithAllOpenGenericHandlerTypesRegistered()
            .WithCustomPipelineBehaviors([
                typeof(CommandBehavior<>),
                typeof(QueryBehavior<,>)
            ]);

        builder.RegisterQuesty(questyConfig.Build());
        builder.RegisterGenericDecorator(typeof(NotificationBehavior<>), typeof(INotificationHandler<>));
        builder.RegisterInstance(NullLoggerFactory.Instance).As<ILoggerFactory>();
        builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>));

        Container = builder.Build();
    }

    public void Dispose() => Container.Dispose();
}
