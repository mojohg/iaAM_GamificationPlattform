// Assets/Scripts/MQTT/SlotCategory.cs
namespace Rocworks.Mqtt
{
    /// <summary>
    /// Kategorien für Topic-Slots (wird im TopicSlotController verwendet).
    /// Die Namen werden 1:1 als Topic-Segment genutzt (ToString()).
    /// </summary>
    public enum SlotCategory
    {
        PROGRESSION,
        TEAM,
        EFFORT,
        ARENA,
        COLLECTION,
        SHOP,
        WELLBEING
    }
}
