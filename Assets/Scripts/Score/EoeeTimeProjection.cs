using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>
/// Four-time visualization projection. It does not calculate eOEE metrics: it
/// only validates and visualizes the scientific evidence published by MWS.
/// Null means unknown, never zero by assumption.
/// </summary>
public sealed class EoeeTimeProjection
{
    public long ObservedDurationMs { get; private set; }
    public long? InterpretationMs { get; private set; }
    public long? ExecutionObservedMs { get; private set; }
    public long? CorrectionMs { get; private set; }
    public long UnclassifiedMs { get; private set; }
    public string MeasurementStatus { get; private set; }

    public float InterpretationFill => (float)(InterpretationMs ?? 0) / ObservedDurationMs;
    public float ExecutionCumulativeFill => (float)((InterpretationMs ?? 0) + (ExecutionObservedMs ?? 0)) / ObservedDurationMs;
    public float CorrectionCumulativeFill => (float)((InterpretationMs ?? 0) + (ExecutionObservedMs ?? 0) + (CorrectionMs ?? 0)) / ObservedDurationMs;
    public float UnclassifiedCumulativeFill => 1f;

    private EoeeTimeProjection() { }

    public static bool TryParseWorkstep(string json, out EoeeTimeProjection result)
    {
        result = null;
        try
        {
            var data = JToken.Parse(json)["data"] as JObject;
            if (data == null) return false;

            var layer = data["eoee_layer3"] as JObject;
            string status = data["measurement_status"]?.ToString()
                         ?? layer?["measurement_status"]?.ToString();
            long? cycle = ReadLong(data["observed_duration_ms"])
                       ?? ReadLong(layer?["cycle_time_ms"]);
            long? ti = ReadLong(layer?["ti_net_ms"]);
            long? execution = ReadLong(data["observed_execution_ms"])
                           ?? ReadLong(layer?["observed_execution_ms"]);
            // A fully segmented occurrence may publish Te only as te_net_ms.
            if (!execution.HasValue && status == "segmented")
                execution = ReadLong(layer?["te_net_ms"]);
            long? correction = ReadLong(layer?["terr_ms"]);
            long? unknown = ReadLong(data["unclassified_duration_ms"])
                         ?? ReadLong(layer?["unclassified_duration_ms"]);

            return TryCreate(cycle, ti, execution, correction, unknown, status, out result);
        }
        catch (Exception) { return false; }
    }

    public static bool TryParsePart(string json, out EoeeTimeProjection result)
    {
        result = null;
        try
        {
            var data = JToken.Parse(json)["data"] as JObject;
            var evaluation = data?["evaluation_results"] as JObject;
            // MWS ProcessPartCompletionUseCase publishes scientific occurrences
            // inside evaluation_results, not as a sibling of evaluation_results.
            var occurrences = evaluation?["scientific_workstep_occurrences"] as JArray
                           ?? data?["scientific_workstep_occurrences"] as JArray;
            if (occurrences == null || occurrences.Count == 0) return false;

            long cycleSum = 0, tiSum = 0, exSum = 0, correctionSum = 0, unknownSum = 0;
            bool allTiKnown = true, allExKnown = true, allCorrectionKnown = true;
            string coverage = "segmented";
            var seenOccurrences = new HashSet<string>();

            foreach (var occurrence in occurrences)
            {
                if (occurrence is not JObject o) return false;
                string snapshotId = o["context_snapshot_id"]?.ToString();
                if (string.IsNullOrWhiteSpace(snapshotId) || !seenOccurrences.Add(snapshotId)) return false;
                var layer = o["eoee_layer3"] as JObject;
                var status = o["measurement_status"]?.ToString();
                long? executionObserved = ReadLong(o["observed_execution_ms"])
                                         ?? ReadLong(layer?["observed_execution_ms"]);
                if (!executionObserved.HasValue && status == "segmented")
                    executionObserved = ReadLong(layer?["te_net_ms"]);
                if (!TryCreate(ReadLong(o["observed_duration_ms"]) ?? ReadLong(layer?["cycle_time_ms"]),
                    ReadLong(layer?["ti_net_ms"]),
                    executionObserved,
                    ReadLong(layer?["terr_ms"]),
                    ReadLong(o["unclassified_duration_ms"]) ?? ReadLong(layer?["unclassified_duration_ms"]),
                    status, out var row)) return false;

                cycleSum = checked(cycleSum + row.ObservedDurationMs);
                unknownSum = checked(unknownSum + row.UnclassifiedMs);
                tiSum = checked(tiSum + (row.InterpretationMs ?? 0));
                exSum = checked(exSum + (row.ExecutionObservedMs ?? 0));
                correctionSum = checked(correctionSum + (row.CorrectionMs ?? 0));
                allTiKnown &= row.InterpretationMs.HasValue;
                allExKnown &= row.ExecutionObservedMs.HasValue;
                allCorrectionKnown &= row.CorrectionMs.HasValue;
                if (row.MeasurementStatus != "segmented") coverage = "partially_observed";
            }

            result = new EoeeTimeProjection
            {
                ObservedDurationMs = cycleSum,
                InterpretationMs = allTiKnown ? tiSum : (long?)null,
                ExecutionObservedMs = allExKnown ? exSum : (long?)null,
                CorrectionMs = allCorrectionKnown ? correctionSum : (long?)null,
                UnclassifiedMs = unknownSum,
                MeasurementStatus = coverage
            };
            // Part with mixed coverage keeps known portions in bars but does
            // not claim complete per-category totals in their text labels.
            result._visualTiMs = tiSum;
            result._visualExMs = exSum;
            result._visualCorrectionMs = correctionSum;
            return true;
        }
        catch (Exception) { return false; }
    }

    private long? _visualTiMs;
    private long? _visualExMs;
    private long? _visualCorrectionMs;
    public float VisualInterpretationFill => (float)(_visualTiMs ?? InterpretationMs ?? 0) / ObservedDurationMs;
    public float VisualExecutionFill => (float)((_visualTiMs ?? InterpretationMs ?? 0) + (_visualExMs ?? ExecutionObservedMs ?? 0)) / ObservedDurationMs;
    public float VisualCorrectionFill => (float)((_visualTiMs ?? InterpretationMs ?? 0) + (_visualExMs ?? ExecutionObservedMs ?? 0) + (_visualCorrectionMs ?? CorrectionMs ?? 0)) / ObservedDurationMs;

    private static bool TryCreate(long? cycle, long? ti, long? execution,
        long? correction, long? unclassified, string status, out EoeeTimeProjection result)
    {
        result = null;
        if (!cycle.HasValue || cycle <= 0 || !unclassified.HasValue ||
            unclassified < 0 || (ti.HasValue && ti < 0) ||
            (execution.HasValue && execution < 0) ||
            (correction.HasValue && correction < 0)) return false;

        if (status != "segmented" && status != "partially_observed" &&
            status != "unsegmented") return false;

        long sum = checked((ti ?? 0) + (execution ?? 0) + (correction ?? 0) + unclassified.Value);
        if (sum != cycle.Value) return false;

        result = new EoeeTimeProjection
        {
            ObservedDurationMs = cycle.Value,
            InterpretationMs = ti,
            ExecutionObservedMs = execution,
            CorrectionMs = correction,
            UnclassifiedMs = unclassified.Value,
            MeasurementStatus = status
        };
        return true;
    }

    private static long? ReadLong(JToken value)
    {
        if (value == null || value.Type == JTokenType.Null) return null;
        return value.Type == JTokenType.Integer ? value.Value<long>() : (long?)null;
    }
}
