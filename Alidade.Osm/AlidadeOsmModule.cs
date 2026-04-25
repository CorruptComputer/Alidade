using Alidade.Osm.Services;
using Autofac;

namespace Alidade.Osm;

/// <summary>
///   Autofac module that registers all <c>Alidade.Osm</c> services. Load this
///   module from the host application's container builder to wire up the OSM API
///   client layer and edit buffer state.
/// </summary>
public class AlidadeOsmModule : Module
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<EditBufferStateService>().AsSelf().SingleInstance();
        builder.RegisterType<OsmCacheService>().As<IOsmCacheService>().SingleInstance();

        builder.RegisterType<PresetService>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<NsiService>().AsSelf().InstancePerLifetimeScope();

        builder.RegisterType<OsmApiContext>().As<IOsmApiContext>().InstancePerLifetimeScope();
        builder.RegisterType<OsmEditingService>().As<IOsmEditingService>().InstancePerLifetimeScope();
        builder.RegisterType<OsmNotesService>().As<IOsmNotesService>().InstancePerLifetimeScope();
        builder.RegisterType<OsmOAuthClient>().AsSelf().InstancePerLifetimeScope();
    }
}
