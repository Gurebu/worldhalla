using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Worldhalla.Plugin.UI;

// What the create-world dialog takes over from vanilla's own create-world panel. Like the palette it copies once, at
// construction, and it must be constructed before the panel is first shown: its labels hold their localization keys
// only until then.
public sealed class VanillaCreateWorldPanel
{
    private const string CANCEL = "Cancel";
    private const string GENERATE_SEED = "GenerateSeed";
    private const string NAME_LABEL = "name";
    private const string NAME_TIP = "name_tip";
    private const string SEED_LABEL = "seed";

    private readonly Transform original;
    private readonly GameObject frame;
    private readonly GameObject? nameTip;

    public VanillaCreateWorldPanel(FejdStartup startup)
    {
        var shelf = GuiElementPalette.NewShelf(startup, "WorldhallaCreateWorldPanel");

        original = startup.m_newWorldName.transform.parent;
        Cancel = original.Find(CANCEL).GetComponent<Button>();
        NameLabelKey = original.Find(NAME_LABEL).GetComponent<TMP_Text>().text;
        SeedLabelKey = original.Find(SEED_LABEL).GetComponent<TMP_Text>().text;

        if (original.Find(NAME_TIP) is { } tip)
            nameTip = Object.Instantiate(tip.gameObject, shelf);

        frame = Object.Instantiate(original.gameObject, shelf);
        foreach (var child in new[] { startup.m_newWorldName.name, startup.m_newWorldSeed.name, startup.m_newWorldDone.name, CANCEL, GENERATE_SEED,
                     NAME_LABEL, NAME_TIP, SEED_LABEL })
        {
            if (frame.transform.Find(child) is { } copy)
                Object.DestroyImmediate(copy.gameObject);
        }
    }

    // Vanilla's own, not a copy: the dialog moves it into its panel so vanilla's show/hide paths keep working
    public Button Cancel { get; }

    public string NameLabelKey { get; }

    public string SeedLabelKey { get; }

    // The panel and its decoration, without vanilla's controls and the labels above them
    public RectTransform Frame(Transform parent, string name)
    {
        var copy = (RectTransform)Object.Instantiate(frame, parent).transform;
        copy.name = name;
        return copy;
    }

    public TMP_Text? NameTip(Transform parent)
    {
        return nameTip != null ? Object.Instantiate(nameTip, parent).GetComponent<TMP_Text>() : null;
    }

    public void HideOriginal()
    {
        original.gameObject.SetActive(false);
    }
}
