using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Map;

/// <summary>
///   High-performance GeoJSON serializer for the NTS geometry types Alidade produces
///   (<see cref="Point"/>, <see cref="LineString"/>, <see cref="Polygon"/>,
///   <see cref="MultiPolygon"/>). Uses <see cref="Utf8JsonWriter"/> directly to avoid
///   the reflection overhead of <c>GeoJsonConverterFactory</c>.
/// </summary>
internal sealed class AlidadeGeoJsonConverter : JsonConverter<FeatureCollection>
{
    /// <inheritdoc />
    public override FeatureCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, FeatureCollection value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "FeatureCollection");
        writer.WriteStartArray("features");

        foreach (IFeature feature in value)
        {
            WriteFeature(writer, feature);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteFeature(Utf8JsonWriter writer, IFeature feature)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "Feature");
        writer.WritePropertyName("geometry");
        WriteGeometry(writer, feature.Geometry);
        writer.WritePropertyName("properties");
        WriteProperties(writer, feature.Attributes);
        writer.WriteEndObject();
    }

    private static void WriteGeometry(Utf8JsonWriter writer, Geometry geometry)
    {
        writer.WriteStartObject();

        switch (geometry)
        {
            case Point p:
                writer.WriteString("type", "Point");
                writer.WritePropertyName("coordinates");
                writer.WriteStartArray();
                writer.WriteNumberValue(p.X);
                writer.WriteNumberValue(p.Y);
                writer.WriteEndArray();
                break;

            case LineString ls:
                writer.WriteString("type", "LineString");
                writer.WritePropertyName("coordinates");
                WriteCoordinateArray(writer, ls.Coordinates);
                break;

            case Polygon poly:
                writer.WriteString("type", "Polygon");
                writer.WritePropertyName("coordinates");
                writer.WriteStartArray();
                WriteCoordinateArray(writer, poly.ExteriorRing.Coordinates);
                foreach (Geometry hole in poly.InteriorRings)
                {
                    WriteCoordinateArray(writer, hole.Coordinates);
                }
                writer.WriteEndArray();
                break;

            case MultiPolygon mp:
                writer.WriteString("type", "MultiPolygon");
                writer.WritePropertyName("coordinates");
                writer.WriteStartArray();

                foreach (Polygon part in mp.Geometries.Cast<Polygon>())
                {
                    writer.WriteStartArray();
                    WriteCoordinateArray(writer, part.ExteriorRing.Coordinates);
                    foreach (Geometry hole in part.InteriorRings)
                    {
                        WriteCoordinateArray(writer, hole.Coordinates);
                    }
                    writer.WriteEndArray();
                }

                writer.WriteEndArray();
                break;
        }

        writer.WriteEndObject();
    }

    private static void WriteCoordinateArray(Utf8JsonWriter writer, Coordinate[] coords)
    {
        writer.WriteStartArray();

        foreach (Coordinate c in coords)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(c.X);
            writer.WriteNumberValue(c.Y);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
    }

    private static void WriteProperties(Utf8JsonWriter writer, IAttributesTable attrs)
    {
        writer.WriteStartObject();

        foreach (string name in attrs.GetNames())
        {
            writer.WritePropertyName(name);

            switch (attrs[name])
            {
                case null:
                    writer.WriteNullValue();
                    break;

                case string s:
                    writer.WriteStringValue(s);
                    break;

                case int i:
                    writer.WriteNumberValue(i);
                    break;

                case long l:
                    writer.WriteNumberValue(l);
                    break;

                case double d:
                    writer.WriteNumberValue(d);
                    break;

                case bool b:
                    writer.WriteBooleanValue(b);
                    break;

                default:
                    writer.WriteStringValue(attrs[name]?.ToString());
                    break;
            }
        }

        writer.WriteEndObject();
    }
}
