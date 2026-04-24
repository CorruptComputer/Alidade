using Autofac;

namespace Alidade.Map;

/// <summary>
///   Autofac module that registers the MapLibre interop service.
///   Load this module from the host application's DI configuration.
/// </summary>
public class AlidadeMapModule : Module
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<MapInteropService>().AsSelf().SingleInstance();
    }
}
