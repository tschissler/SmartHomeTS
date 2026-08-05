namespace SharedContracts
{
    /// <summary>
    /// Status message of the MixerController firmware, published on
    /// daten/Heizung/{location}/Mischersteuerung/{mixer}.
    /// Position values: "open", "closed", "partial", "unknown".
    /// Target is always the last commanded full-travel position ("open" or
    /// "closed") — while the step controller holds an intermediate position,
    /// Position is "partial" but Target keeps the base command.
    /// OpenPercent (100 = fully open) is the firmware's travel-time estimate;
    /// absent while no reference run has established the position.
    /// </summary>
    public record MixerStatusData(
        string Position,
        string Target,
        bool Moving,
        string Timestamp,
        int? OpenPercent = null);
}
