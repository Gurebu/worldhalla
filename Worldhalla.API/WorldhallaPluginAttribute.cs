using JetBrains.Annotations;

namespace Worldhalla.API;

[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class WorldhallaPluginAttribute : Attribute;
