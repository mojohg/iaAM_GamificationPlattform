using System;
using System.IO;
using UnityEngine;

namespace Rocworks.Mqtt
{
    /// <summary>
    /// Globale Topic-Root-Verwaltung mit Early-Init.
    /// Lädt (falls vorhanden) den Root bereits VOR dem ersten Scene-Start
    /// aus der lokalen Config-Datei, so dass alle Start()/Awake() schon
    /// den richtigen Root sehen.
    /// </summary>
    public static class TopicNamespace
    {
        public static event Action<string, string> OnRootChanged;

        /// <summary>Wird gefeuert, sobald der Root das erste Mal valide gesetzt wurde.</summary>
        public static event Action OnRootReady;

        private static string _currentRoot = "DEFAULT/0001/MG/ST01/AGENT";
        public static string CurrentRoot => _currentRoot;

        /// <summary>True, sobald der Root einmal sauber initialisiert wurde.</summary>
        public static bool IsReady { get; private set; }

        // Pfad zur gleichen Datei wie im ConfigUIController
        private static string ConfigPath =>
            Path.Combine(Application.persistentDataPath, "topic_config.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EarlyInit()
        {
            // 1) Versuche Root aus lokaler Datei zu laden (falls vorhanden)
            var loaded = TryLoadRootFromDisk(out var rootFromDisk);

            if (loaded && !string.IsNullOrWhiteSpace(rootFromDisk))
            {
                _currentRoot = Normalize(rootFromDisk);
                IsReady = true;
                OnRootReady?.Invoke();
                Debug.Log($"[TopicNamespace] EarlyInit Root from disk: {_currentRoot}");
                return;
            }

            // 2) Falls nichts auf Disk → Defaults beibehalten
            _currentRoot = Normalize(_currentRoot);
            IsReady = true;
            OnRootReady?.Invoke();
            Debug.Log($"[TopicNamespace] EarlyInit using defaults: {_currentRoot}");
        }

        public static void SetRoot(string newRoot)
        {
            if (string.IsNullOrWhiteSpace(newRoot)) return;
            newRoot = Normalize(newRoot);

            if (string.Equals(_currentRoot, newRoot, StringComparison.Ordinal))
                return;

            var old = _currentRoot;
            _currentRoot = newRoot;
            IsReady = true; // sicherheitshalber

            OnRootChanged?.Invoke(old, _currentRoot);
            if (old == null) OnRootReady?.Invoke();

            Debug.Log($"[TopicNamespace] Root changed: {old} -> {_currentRoot}");
        }

        public static string TopicFor(params string[] parts)
        {
            if (parts == null || parts.Length == 0) return _currentRoot;
            if (string.IsNullOrWhiteSpace(_currentRoot)) return string.Join("/", parts);
            return _currentRoot + "/" + string.Join("/", parts);
        }

        private static bool TryLoadRootFromDisk(out string root)
        {
            root = null;
            try
            {
                if (!File.Exists(ConfigPath))
                    return false;

                // sehr schlanke DTO-Struktur, nur das was wir brauchen
                var json = File.ReadAllText(ConfigPath);
                var data = JsonUtility.FromJson<LocalData>(json);
                if (data == null) return false;

                root = $"{data.company}/{data.line}/{data.machineGroup}/{data.stationId}/{data.agentversion}";
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TopicNamespace] Could not load root from disk: {ex.Message}");
                return false;
            }
        }

        private static string Normalize(string s) => s?.Trim().Trim('/');

        [Serializable]
        private class LocalData
        {
            public string company;
            public string line;
            public string machineGroup;
            public string stationId;
            public string agentversion;
            // mqtt-Felder sind hier egal
        }
    }
}
