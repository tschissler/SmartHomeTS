namespace SmartHome.Web.Services;

/// <summary>
/// Die Topics der Beleuchtung, die der Konvention aus <c>Docs/MQTT-Topic-Konvention.md</c>
/// folgen: <c>art / Kategorie / [Ort /] Geraet / Aspekt</c>, deutsch ab der Kategorie.
/// </summary>
/// <remarks>
/// Die beiden bestehenden Topics der Seite (<c>commands/illumination/LEDStripe/setColor</c>
/// und <c>commands/shelly/Lampe</c>) sind Altbestand und stehen bewusst nicht hier. Ihre
/// Migration ist keine Anpassung der Web-App allein: Auf dem Farb-Topic hoert die
/// LEDStripe-Firmware mit, und die Konvention verlangt bei Geraeten, die nicht gleichzeitig
/// aktualisiert werden koennen, einen abgestimmten Schnitt statt eines Alleingangs.
/// </remarks>
public static class BeleuchtungTopics
{
    /// <summary>
    /// Die vom Nutzer gespeicherten Szenenwerte der Schrankbeleuchtung. Retained, weil es
    /// Zustand ist und kein Ereignis.
    /// </summary>
    /// <remarks>
    /// Herleitung der fuenf Ebenen:
    /// <list type="bullet">
    /// <item><c>konfiguration</c> - gespeicherte Einstellung, weder Messwert noch Kommando.</item>
    /// <item><c>Beleuchtung</c> - die fachliche Domaene, deutsch ab der Kategorie.</item>
    /// <item><c>M1</c> - der Streifen haengt fest im Wohnzimmerschrank; die Firmware meldet
    /// ihren Heartbeat selbst unter diesem Ort. Der Ort ist laut Konvention Pflicht fuer
    /// ortsgebundene Geraete und wird einmal je Kategorie entschieden, nicht je Nachricht.</item>
    /// <item><c>Schrank</c> - das konkrete Objekt. Nicht <c>LEDStripe</c>: Die Konvention
    /// verbietet den Typ im Namen, und ein LED-Streifen ist eine Bauart, kein Objekt.</item>
    /// <item><c>Szenen</c> - was von diesem Objekt.</item>
    /// </list>
    /// </remarks>
    public const string Szenen = "konfiguration/Beleuchtung/M1/Schrank/Szenen";
}

/// <summary>
/// Was der Nutzer ueber die im Code stehenden Szenen gespeichert hat.
/// </summary>
/// <remarks>
/// <para>
/// Gespeichert werden <b>nur Abweichungen</b>, und zwar unter einem stabilen Schluessel je
/// Szene - nicht die ganze Liste. Das ist der Unterschied, auf den es ankommt: Ein retained
/// Sechs-Element-Array wuerde die Code-Vorgabe dauerhaft ersetzen, und eine siebte Szene,
/// die spaeter im Code dazukommt, erschiene nie, weil das Topic sie nicht kennt und gewinnt.
/// </para>
/// <para>
/// Mit dem Schluessel als Bezug gilt: Der Code bestimmt, <i>welche</i> Szenen es gibt und in
/// welcher Reihenfolge; das Topic bestimmt nur <i>die Werte</i> derer, die der Nutzer
/// angefasst hat. Eine neue Szene erscheint sofort, eine umbenannte behaelt ihre
/// gespeicherten Werte (der Anzeigename ist nicht der Schluessel), und der Eintrag einer
/// geloeschten Szene bleibt als wirkungsloser Rest liegen, statt etwas kaputtzumachen.
/// </para>
/// </remarks>
public sealed class SzenenVorgaben
{
    /// <summary>Pflichtfeld der Konvention: ohne ihn ist das Alter eines retained Werts nicht feststellbar.</summary>
    public DateTimeOffset Zeitpunkt { get; set; }

    /// <summary>Abweichungen je Szenenschluessel. Szenen ohne Eintrag behalten ihre Code-Vorgabe.</summary>
    public Dictionary<string, SzenenWerte> Szenen { get; set; } = new();
}

/// <summary>Die vier Werte, die eine Szene ausmachen - dieselben, die die Regler einstellen.</summary>
public sealed class SzenenWerte
{
    public int Farbton { get; set; }
    public int Saettigung { get; set; }
    public int Helligkeit { get; set; }
    public int Dichte { get; set; }
}
