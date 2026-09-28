using JetBrains.Annotations;

namespace Worldhalla.API.Attributes;

[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class WorldhallaPluginAttribute : Attribute;
