using Autofac;

namespace Alidade.Core;

/// <summary>
///   Autofac module that registers <c>Alidade.Core</c> services.
/// </summary>
public sealed class AlidadeCoreModule : Module
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<SettingsStateService>().AsSelf().SingleInstance();
    }
}
