using System;
using UnityEngine;

public static class TopicSettings
{
    private static TopicRootConfig _cfg;
    public static event Action OnChanged;

    /// <summary>Greift auf die zentrale Topic-Config zu (Resources/TopicRootConfig.asset)</summary>
    public static TopicRootConfig Config
    {
        get
        {
            if (_cfg == null)
            {
                _cfg = Resources.Load<TopicRootConfig>("TopicRootConfig");
                if (_cfg == null)
                    _cfg = ScriptableObject.CreateInstance<TopicRootConfig>(); // Runtime-Defaults
                    Debug.LogError("TopicRootConfig.asset nicht gefunden. Nutze Runtime-Defaults.");
            }
            return _cfg;
        }
    }

    /// <summary>Convenience: Root-Pfad, z.B. FHAACHEN/LEANLAB/MWS/0001/MULTIAGENT</summary>
    public static string Root => Config != null ? Config.Root : string.Empty;

    /// <summary>Für ein späteres UI (zur Laufzeit Werte setzen, ohne Asset zu überschreiben)</summary>
    public static void ApplyRuntimeOverride(
        string company = null, string line = null, string machineGroup = null,
        string stationId = null, string agentversion = null)
    {
        if (Config == null) return;

        // Laufzeit-Override: Wir merken uns nur im Speicher neue Werte
        Config.company     = company     ?? Config.company;
        Config.line          = line          ?? Config.line;
        Config.machineGroup = machineGroup ?? Config.machineGroup;
        Config.stationId    = stationId    ?? Config.stationId;
        Config.agentversion        = agentversion        ?? Config.agentversion;

        OnChanged?.Invoke();
    }
}
