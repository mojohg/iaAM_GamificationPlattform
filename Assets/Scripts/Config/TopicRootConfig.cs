using UnityEngine;

[CreateAssetMenu(
    fileName = "TopicRootConfig",
    menuName = "MQTT/Topic Root Config",
    order = 0)]
public class TopicRootConfig : ScriptableObject
{
    [Header("Hier stellst du deinen Namensraum ein")]
    public string company = "FHAACHEN";    // z.B. FH, RWTH …
    public string line = "LEANLAB";          // Labor/Area
    public string machineGroup = "MWS";     // Maschinengruppe
    public string stationId = "0001";       // Station / Zelle
    public string agentversion = "MULTIAGENT";     // Agent/Subsystem

    /// <summary>z.B. FHAACHEN/LEANLAB/MWS/0001/MULTIAGENT</summary>
    public string Root =>
        $"{company}/{line}/{machineGroup}/{stationId}/{agentversion}";
}
