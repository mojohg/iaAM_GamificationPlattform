using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>Server-owned badge collection and once-only unlock notification state.</summary>
public sealed class BadgeProgressionState
{
    private const int Levels = 8;
    private const int Difficulties = 7;
    private readonly HashSet<int> _unlockedSlots = new HashSet<int>();
    private readonly HashSet<string> _announcedBadges = new HashSet<string>();
    public int UnlockedCount => _unlockedSlots.Count;
    public int TotalCount => Levels * Difficulties;

    public bool TryApplySnapshot(string json)
    {
        JArray array;
        try { array = JArray.Parse(json); }
        catch (Exception) { return false; }
        var updated = new HashSet<int>();
        foreach (var badge in array)
        {
            int level = badge["level"]?.Value<int>() ?? 0;
            int difficulty = badge["difficulty"]?.Value<int>() ?? 0;
            if (badge["is_unlocked"]?.Value<bool>() == true && IsValid(level, difficulty))
                updated.Add((level - 1) * Difficulties + difficulty - 1);
        }
        _unlockedSlots.Clear();
        _unlockedSlots.UnionWith(updated);
        return true;
    }

    public bool TryApplyUnlock(string json, out string label)
    {
        label = null;
        try
        {
            var root = JObject.Parse(json);
            var data = root["data"] as JObject;
            if (data == null || data["event"]?.ToString() != "badge_unlocked" ||
                data["result"]?.ToString() != "success") return false;

            int userId = data["user_id"]?.Value<int>() ?? 0;
            int badgeId = data["badge_id"]?.Value<int>() ?? 0;
            int level = data["level"]?.Value<int>() ?? 0;
            int difficulty = data["difficulty"]?.Value<int>() ?? 0;
            if (userId <= 0 || badgeId <= 0 || !IsValid(level, difficulty)) return false;

            string eventKey = $"{userId}:{badgeId}";
            if (!_announcedBadges.Add(eventKey)) return false;
            _unlockedSlots.Add((level - 1) * Difficulties + difficulty - 1);
            label = $"Badge freigeschaltet · Level {level} · {data["difficulty_label"]?.ToString() ?? difficulty.ToString()}";
            return true;
        }
        catch (Exception) { return false; }
    }

    private static bool IsValid(int level, int difficulty) =>
        level >= 1 && level <= Levels && difficulty >= 1 && difficulty <= Difficulties;
}
