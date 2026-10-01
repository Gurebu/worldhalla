using BepInEx;

namespace GeneratorSample;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInDependency("dev.gurebu.worldhalla")]
public class GeneratorSamplePlugin : BaseUnityPlugin
{
    public const string GUID = "com.example.generatorsample";
    public const string NAME = "Generator Sample";
    public const string VERSION = "1.0.0";
}
