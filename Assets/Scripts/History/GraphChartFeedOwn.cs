#define Graph_And_Chart_PRO
using UnityEngine;
using ChartAndGraph;
using System.Collections.Generic;
using UnityEngine.UI;
using GraphWorkaround;

public class GraphChartFeedOwn : MonoBehaviour
{
    [SerializeField] private HistoryController historyController;
    [SerializeField] private Text axisTitle;

    // technische Keys -> Anzeige-Namen
    private readonly List<(string key, string label)> order = new()
    {
        ("quality", "Quality"),
        ("total_interpretation_time", "Interpretation Time"),
        ("total_processing_time", "Processing Time"),
        ("total_error_correction_time", "Error Correction Time"),
        ("total_error_count", "Error Count"),
        ("score", "Score")
    };

    private int currentIdx = 0;
    private GraphChartBase graph;

    void Start()
    {
        graph = GetComponent<GraphChartBase>();
        graph.Scrollable = true;
        graph.HorizontalValueToStringMap.Clear();

        if (historyController != null)
            historyController.OnHistoryDataProcessed.AddListener(OnHistoryDataReceived);
    }

   void OnHistoryDataReceived(Dictionary<string, List<float>> data, List<string> timestamps)
{
    if (currentIdx < 0 || currentIdx >= order.Count) currentIdx = 0;

    var (key, display) = order[currentIdx];
    UpdateAxisTitle(display);

    if (!data.TryGetValue(key, out var series) || series == null || series.Count == 0)
    {
        graph.DataSource.StartBatch();
        foreach (var pair in order) graph.DataSource.ClearCategory(pair.label);
        graph.HorizontalValueToStringMap.Clear();
        graph.DataSource.EndBatch();
        return;
    }

    graph.DataSource.StartBatch();

    // nur die aktive Kategorie zeichnen
    foreach (var pair in order) graph.DataSource.ClearCategory(pair.label);

    // ACHTUNG: Keys sind double, nicht string
    graph.HorizontalValueToStringMap.Clear();
    for (int i = 0; i < series.Count; i++)
    {
        if (i < timestamps.Count && !string.IsNullOrEmpty(timestamps[i]))
            graph.HorizontalValueToStringMap[(double)i] = timestamps[i]; // <-- hier lag der Fehler
    }

    for (int i = 0; i < series.Count; i++)
    {
        double x = i;                 // X als double
        double y = series[i];         // Y wird implizit zu double
        graph.DataSource.AddPointToCategory(display, x, y);
    }

    graph.DataSource.MakeCurveCategorySmooth(display);
    graph.DataSource.EndBatch();
}

    public void NextCategory()
    {
        currentIdx = (currentIdx + 1) % order.Count;
        if (historyController != null)
            OnHistoryDataReceived(historyController.LatestHistoryData, historyController.FormattedTimestamps);
    }

    public void PreviousCategory()
    {
        currentIdx = (currentIdx - 1 + order.Count) % order.Count;
        if (historyController != null)
            OnHistoryDataReceived(historyController.LatestHistoryData, historyController.FormattedTimestamps);
    }

    void UpdateAxisTitle(string displayCategory)
    {
        if (axisTitle) axisTitle.text = $"Category: {displayCategory}";
    }
}
