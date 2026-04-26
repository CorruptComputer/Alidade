using System.Text.Json;
using Autofac;
using NetTopologySuite.IO.Converters;

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

        // OsmGeoJsonConverter is listed first so it takes precedence for FeatureCollection;
        // GeoJsonConverterFactory remains as fallback for any other NTS types.
        builder.RegisterInstance(new JsonSerializerOptions
        {
            Converters = { new AlidadeGeoJsonConverter(), new GeoJsonConverterFactory() }
        }).AsSelf().SingleInstance();
    }
}
