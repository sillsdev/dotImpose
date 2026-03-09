using System.Reflection;

namespace DotImpose
{
    /// <summary>
    /// Provides runtime identity details for the loaded dotImpose assembly.
    /// </summary>
    public static class DotImposeRuntimeInfo
    {
        /// <summary>
        /// Returns the loaded assembly's informational version.
        /// </summary>
        public static string GetInformationalVersion()
        {
            var assembly = typeof(DotImposeRuntimeInfo).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "unknown";
        }

        /// <summary>
        /// Returns the loaded assembly file path.
        /// </summary>
        public static string GetAssemblyPath()
        {
            return typeof(DotImposeRuntimeInfo).Assembly.Location;
        }
    }
}
