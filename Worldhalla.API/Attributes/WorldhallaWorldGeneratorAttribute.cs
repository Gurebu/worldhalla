using JetBrains.Annotations;

namespace Worldhalla.API.Attributes;

[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class WorldhallaWorldGeneratorAttribute(string guid) : Attribute
{
    public string Guid { get; } = guid;
}
