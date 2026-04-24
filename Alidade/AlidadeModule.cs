using System.Text.Json;
using Autofac;
using Alidade.Core;
using Alidade.Interop;
using Alidade.Map;
using Alidade.Osm;
using NetTopologySuite.IO.Converters;
using Questy.Autofac;
using Questy.Autofac.Builder;
using Alidade.Core.PipelineBehaviors;

namespace Alidade;

/// <summary>
///   Autofac module for Alidade
/// </summary>
public class AlidadeModule(string baseAddress) : Module
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        // HTTP client
        builder.Register(_ => new HttpClient { BaseAddress = new Uri(baseAddress) })
               .AsSelf()
               .InstancePerLifetimeScope();

        // Modules
        builder.RegisterModule<AlidadeCoreModule>();
        builder.RegisterModule<AlidadeOsmModule>();
        builder.RegisterModule<AlidadeMapModule>();

        // State services
        builder.RegisterType<MapStateService>().AsSelf().SingleInstance();
        builder.RegisterType<SelectionStateService>().AsSelf().SingleInstance();
        builder.RegisterType<ToolStateService>().AsSelf().SingleInstance();
        builder.RegisterType<UndoStateService>().AsSelf().SingleInstance();
        builder.RegisterType<ValidationStateService>().AsSelf().SingleInstance();
        builder.RegisterType<DraftStateService>().AsSelf().SingleInstance();
        builder.RegisterType<AuthStateService>().AsSelf().SingleInstance();

        // Application services
        builder.RegisterType<IndexedDbInteropService>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<IndexedDBService>().AsSelf().As<IStorageService>().InstancePerLifetimeScope();
        builder.RegisterType<EditBufferService>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<DrawingToolService>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<ValidationService>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<AuthService>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<GpxService>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<ImageryService>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<SettingsService>().AsSelf().InstancePerLifetimeScope();

        // NTS GeoJSON serialization options, used when pushing FeatureCollections to MapLibre
        builder.RegisterInstance(new JsonSerializerOptions
        {
            Converters = { new GeoJsonConverterFactory() }
        }).AsSelf().SingleInstance();

        // Scan this assembly, Alidade.Map, and Alidade.Osm for Questy handlers
        QuestyConfigurationBuilder questyConfig = QuestyConfigurationBuilder
            .Create(ThisAssembly, typeof(AlidadeMapModule).Assembly, typeof(AlidadeOsmModule).Assembly)
            .WithAllOpenGenericHandlerTypesRegistered()
            .WithCustomPipelineBehaviors([
                typeof(UndoPipelineBehavior<,>),
                typeof(CommandBehavior<>),
                typeof(QueryBehavior<,>)
            ]);

        builder.RegisterQuesty(questyConfig.Build());

        builder.RegisterGenericDecorator(typeof(NotificationBehavior<>), typeof(INotificationHandler<>));
    }
}
