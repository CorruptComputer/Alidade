using System.Reflection;

namespace Alidade.Models;

/// <summary>
///   Exposes build-time version information sourced from the assembly's
///   <see cref="AssemblyInformationalVersionAttribute"/>. The informational
///   version is set by <c>Directory.Build.props</c> as <c>VersionPrefix-VersionSuffix</c>
///   (e.g. <c>0.0.1-develop</c> in debug, <c>0.0.1-abcde123</c> in release).
/// </summary>
public static class AppInfo
{
    /// <summary>
    ///   The full version string, e.g. <c>0.0.1-develop</c> or <c>0.0.1-abcde123</c>.
    /// </summary>
    public static readonly string Version
        = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "(null)"; // Shouldn't ever really happen, but as a fallback

    /// <summary>
    ///   The formatted user-agent string used in OSM changeset <c>created_by</c> tags,
    ///   e.g. <c>Alidade/0.0.1-develop</c>.
    /// </summary>
    public static readonly string UserAgent = $"Alidade/{Version}";
}
