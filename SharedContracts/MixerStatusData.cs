namespace SharedContracts
{
    /// <summary>
    /// Status message of the MixerController firmware, published on
    /// daten/Heizung/{location}/Mischersteuerung/{mixer}.
    /// Position/Target values: "open", "closed", "unknown".
    /// </summary>
    public record MixerStatusData(
        string Position,
        string Target,
        bool Moving,
        string Timestamp);
}
