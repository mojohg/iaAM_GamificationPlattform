// TopicNamespace.cs
public static class TopicNamespace
{
    // Fallback-Root ohne letzten Slash ändern? -> hier.
    private const string DEFAULT_ROOT = "FHAACHEN/LEANLAB/MWS/0001/MULTIAGENT/";

    private static string _root = DEFAULT_ROOT;

    /// <summary>Setzt den Root-Pfad (z. B. später aus deinem Config-UI). Ohne Slash am Ende übergeben.</summary>
    public static void ConfigureRoot(string rootWithoutTrailingSlash)
    {
        if (string.IsNullOrWhiteSpace(rootWithoutTrailingSlash))
            _root = DEFAULT_ROOT;
        else
            _root = Normalize(rootWithoutTrailingSlash);
    }

    public static string Root => _root;

    /// <summary>z. B. GroupRoot("RECOMMANDATION") => ".../MULTIAGENT/RECOMMANDATION/"</summary>
    public static string GroupRoot(string group) => Join(Root, group, endWithSlash: true);

    /// <summary>z. B. TopicFor("RECOMMANDATION", "PROGRESSION")</summary>
    public static string TopicFor(string group, string category) => Join(Root, group, category);

    /// <summary>Wildcard für eine Gruppe, z. B. ".../RECOMMANDATION/#"</summary>
    public static string WildcardFor(string group) => GroupRoot(group) + "#";

    // -------- utils --------
    private static string Normalize(string s)
    {
        s = s.Trim().Trim('/');
        return s + "/";
    }

    private static string Join(string root, string seg1, bool endWithSlash = false)
    {
        var r = $"{root.TrimEnd('/')}/{seg1.Trim('/').ToUpperInvariant()}";
        return endWithSlash ? r + "/" : r;
    }

    private static string Join(string root, string seg1, string seg2)
        => $"{root.TrimEnd('/')}/{seg1.Trim('/').ToUpperInvariant()}/{seg2.Trim('/').ToUpperInvariant()}";
}

public enum SlotCategory { PROGRESSION, COLLECTION, SHOP, ARENA, TEAM, WELLBEING }
