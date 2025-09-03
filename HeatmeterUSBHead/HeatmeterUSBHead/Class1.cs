using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var p = ParseArgs(args);
        if (!p.TryGetValue("port", out var port))
        {
            Console.WriteLine("Bitte Port angeben, z.B. --port COM9");
            Console.WriteLine("Verfügbare Ports: " + string.Join(", ", SerialPort.GetPortNames()));
            return 1;
        }

        int baud = GetInt(p, "baud", 2400);
        int preLen = GetInt(p, "preamble", 504);
        int readMs = GetInt(p, "readms", 6000);
        bool dtr = GetBool(p, "dtr", false);
        bool rts = GetBool(p, "rts", false);
        bool probe = GetBool(p, "probe", true);
        string serial = p.TryGetValue("serial", out var s) ? s : "51158148";

        Console.WriteLine($"Port={port} Baud={baud} Preamble={preLen} DTR={dtr} RTS={rts} Probe={probe} Serial={serial}");

        try
        {
            if (probe)
            {
                // 1) SND_NKE (Broadcast 0xFE)
                SendPreamble(port, baud, dtr, rts, preLen);
                using (var sp1 = Open(port, baud, Parity.Even, 8, StopBits.One, dtr, rts, 800))
                {
                    SendShort(sp1, 0x40, 0xFE); // SND_NKE
                    TryReadAck(sp1, "SND_NKE");
                }

                // 2) Select by Secondary (A=0xFD, CI=0x52)
                SendPreamble(port, baud, dtr, rts, preLen);
                using (var sp2 = Open(port, baud, Parity.Even, 8, StopBits.One, dtr, rts, 1200))
                {
                    var sel = string.IsNullOrWhiteSpace(serial) ? BuildSelectWildcard() : BuildSelectBySecondary(serial);
                    sp2.Write(sel, 0, sel.Length);
                    Console.WriteLine(WaitAck(sp2, 1200) ? "Select: ACK 0xE5" : "Select: kein ACK");
                }
            }

            // 3) REQ_UD2 → Long-Frame lesen & dekodieren
            SendPreamble(port, baud, dtr, rts, preLen);
            using (var sp = Open(port, baud, Parity.Even, 8, StopBits.One, dtr, rts, 3000))
            {
                SendShort(sp, 0x5B, 0xFD); // REQ_UD2 an selektiertes Gerät
                var frame = ReadLongFrame(sp);         // validiert Länge/Checksumme
                DumpFrameSummary(frame);               // Header anzeigen
                DecodeAndPrint(frame);                 // Zählerstände + Momentanwerte
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Fehler: " + ex.Message);
            return 2;
        }
    }

    // ==== Wake-up (8N1) ====
    static void SendPreamble(string port, int baud, bool dtr, bool rts, int count)
    {
        using var sp = new SerialPort(port, baud, Parity.None, 8, StopBits.One)
        { DtrEnable = dtr, RtsEnable = rts, WriteTimeout = 3000 };
        sp.Open();
        sp.DiscardInBuffer(); sp.DiscardOutBuffer();
        var pre = Enumerable.Repeat((byte)0x55, count).ToArray();
        sp.Write(pre, 0, pre.Length);
        sp.BaseStream.Flush();
        Console.WriteLine($"Wake-up: {count} × 0x55 @ 8N1 gesendet.");
        Thread.Sleep(120);
    }

    // ==== Port open (8E1) ====
    static SerialPort Open(string port, int baud, Parity par, int bits, StopBits stop, bool dtr, bool rts, int readTimeout)
    {
        var sp = new SerialPort(port, baud, par, bits, stop)
        { DtrEnable = dtr, RtsEnable = rts, ReadTimeout = readTimeout, WriteTimeout = 3000 };
        sp.Open(); sp.DiscardInBuffer();
        return sp;
    }

    // ==== Short frame TX ====
    static void SendShort(SerialPort sp, byte C, byte A)
    {
        byte cs = (byte)((C + A) & 0xFF);
        var f = new byte[] { 0x10, C, A, cs, 0x16 };
        sp.Write(f, 0, f.Length);
        Console.WriteLine($"TX Short: {BitConverter.ToString(f)}");
    }

    // ==== Select-by-secondary ====
    static byte[] BuildSelectBySecondary(string serial)
    {
        var id4 = EncodeSerialTypeA(serial);
        var user = new List<byte> { 0x53, 0xFD, 0x52 };
        user.AddRange(id4);
        user.Add(0xFF); user.Add(0xFF); // Manufacturer wildcard
        user.Add(0xFF);                 // Version wildcard
        user.Add(0xFF);                 // Medium wildcard
        byte len = (byte)user.Count;
        byte cs = Checksum(user);
        var frame = new List<byte> { 0x68, len, len, 0x68 };
        frame.AddRange(user); frame.Add(cs); frame.Add(0x16);
        Console.WriteLine($"TX Select: {BitConverter.ToString(frame.ToArray())} (Serial={serial})");
        return frame.ToArray();
    }
    static byte[] BuildSelectWildcard()
    {
        var user = new List<byte> { 0x53, 0xFD, 0x52, 0xFF,0xFF,0xFF,0xFF, 0xFF,0xFF, 0xFF, 0xFF };
        byte len = (byte)user.Count;
        byte cs = Checksum(user);
        var frame = new List<byte> { 0x68, len, len, 0x68 };
        frame.AddRange(user); frame.Add(cs); frame.Add(0x16);
        return frame.ToArray();
    }

    // ==== Long frame RX (prüft Länge & Checksumme) ====
    static byte[] ReadLongFrame(SerialPort sp)
    {
        int ReadByteOrThrow()
        {
            int b = sp.ReadByte();
            if (b < 0) throw new TimeoutException();
            return b;
        }

        // sync auf 0x68
        int b;
        do { b = ReadByteOrThrow(); } while (b != 0x68);
        int len1 = ReadByteOrThrow();
        int len2 = ReadByteOrThrow();
        if (len1 != len2) throw new Exception("Ungültige Länge.");
        if (ReadByteOrThrow() != 0x68) throw new Exception("Start2 fehlt.");

        var payload = new byte[len1]; // C, A, CI, UserData
        int n = 0;
        while (n < len1) n += sp.Read(payload, n, len1 - n);

        int cs = ReadByteOrThrow();
        int stop = ReadByteOrThrow();
        if (stop != 0x16) throw new Exception("Stop (0x16) fehlt.");

        byte calc = (byte)(payload.Aggregate(0, (acc, x) => acc + x) & 0xFF);
        if (calc != cs) throw new Exception($"Checksumme falsch. exp={calc:X2} got={cs:X2}");

        Console.WriteLine($"RX Long: len={len1} C=0x{payload[0]:X2} A=0x{payload[1]:X2} CI=0x{payload[2]:X2}");
        return payload;
    }

    // ==== Header-Ausgabe ====
    static void DumpFrameSummary(byte[] payload)
    {
        var id = payload.AsSpan(3, 4).ToArray();
        var man = payload.AsSpan(7, 2).ToArray();
        byte ver = payload[9], med = payload[10], acc = payload[11], sta = payload[12];
        var sig = payload.AsSpan(13, 2).ToArray();

        Console.WriteLine("Header:");
        Console.WriteLine($"  ID: {DecodeSerialHuman(id)}  (raw: {BitConverter.ToString(id)})");
        Console.WriteLine($"  Manufacturer (raw): {BitConverter.ToString(man)}");
        Console.WriteLine($"  Version: {ver}   Medium: 0x{med:X2}   AccessNo: {acc}   Status: 0x{sta:X2}   Sig: {BitConverter.ToString(sig)}");
    }

    // ==== Datensätze (inkl. Zählerstände) ====
    static void DecodeAndPrint(byte[] payload)
    {
        // ab Byte 15 beginnen die Records
        int i = 15;
        var end = payload.Length;

        var readings = new List<string>();   // Zählerstände (Energie/Volumen, inkl. Storage S)
        var instant = new List<string>();    // Momentanwerte

        while (i < end)
        {
            byte dif = payload[i++];

            if (dif == 0x00 || dif == 0x2F) break; // Ende/No data

            // DIFE-Kette: Storage (S), Tariff (T), DeviceUnit (DU)
            int storage = 0;
            int tariff = 0;
            int devUnit = 0;
            int dfeIndex = 0;

            while ((dif & 0x80) != 0 && i < end)  // weitere DIFE?
            {
                byte dfe = payload[i++];
                storage |= (dfe & 0x0F) << (4 * dfeIndex);
                tariff  |= ((dfe >> 4) & 0x03) << (2 * dfeIndex);
                if ((dfe & 0x40) != 0) devUnit++;
                dfeIndex++;
                if ((dfe & 0x80) == 0) break;
            }

            if (i >= end) break;
            byte vif = payload[i++];

            // VIFE-Kette überspringen
            while ((vif & 0x80) != 0 && i < end) { vif = payload[i++]; }

            int len = DataLengthFromDIF(dif & 0x0F);
            if (i + len > end) break;
            var data = payload.Skip(i).Take(len).ToArray();
            i += len;

            // Identnummer (Debug)
            if (len == 4 && vif == 0x78)
            {
                var idTxt = DecodeSerialHuman(data);
                instant.Add($"ID(VIF 0x78): {idTxt}");
                continue;
            }

            // ZÄHLERSTÄNDE
            if (TryMapVifToReading(vif, data, out var label, out var valueStr))
            {
                string sTag = storage > 0 ? $"S{storage}" : "S0";
                readings.Add($"{sTag}: {label} = {valueStr}");
                continue;
            }

            // MOMENTANWERTE
            if (TryMapVifToInstant(vif, data, out var instLabel, out var instVal))
            {
                instant.Add($"{instLabel} = {instVal}");
                continue;
            }

            // Unbekannt -> Rohdump
            if (len > 0)
                instant.Add($"Unbekannt VIF 0x{vif:X2} (len {len}): {BitConverter.ToString(data)}");
        }

        Console.WriteLine("\nZählerstände:");
        if (readings.Count == 0) Console.WriteLine("  (keine bekannten Zählerstände erkannt)");
        else foreach (var r in readings) Console.WriteLine("  " + r);

        Console.WriteLine("\nMomentanwerte:");
        if (instant.Count == 0) Console.WriteLine("  (keine bekannten Momentanwerte erkannt)");
        else foreach (var r in instant) Console.WriteLine("  " + r);
    }

    // --- Mapping ---
    static bool TryMapVifToReading(byte vif, byte[] data, out string label, out string value)
    {
        label = ""; value = "";

        // Energie-Zählerstand
        if (data.Length == 4 && vif == 0x06) // 0.001 MWh
        {
            double v = BitConverter.ToUInt32(data, 0) * 0.001;
            label = "Energie"; value = v.ToString("F3") + " MWh"; return true;
        }
        if (data.Length == 4 && vif == 0x0E) // 0.001 GJ
        {
            double v = BitConverter.ToUInt32(data, 0) * 0.001;
            label = "Energie"; value = v.ToString("F3") + " GJ"; return true;
        }

        // Volumen-Zählerstand
        if (data.Length == 4 && vif == 0x13) // 0.001 m³
        {
            double v = BitConverter.ToUInt32(data, 0) * 0.001;
            label = "Volumen"; value = v.ToString("F3") + " m³"; return true;
        }

        return false;
    }

    static bool TryMapVifToInstant(byte vif, byte[] data, out string label, out string value)
    {
        label = ""; value = "";

        // Leistung / Durchfluss
        if (data.Length == 4 && vif == 0x2B) { double v = BitConverter.ToUInt32(data, 0) * 0.001; label = "Leistung";  value = v.ToString("F3") + " kW";   return true; }
        if (data.Length == 4 && vif == 0x3B) { double v = BitConverter.ToUInt32(data, 0) * 0.001; label = "Durchfluss";value = v.ToString("F3") + " m³/h"; return true; }

        // Temperaturen
        if (data.Length == 2 && vif == 0x5B) { double v = BitConverter.ToUInt16(data, 0);        label = "Vorlauf";  value = v.ToString("F0") + " °C";   return true; }
        if (data.Length == 2 && vif == 0x5F) { double v = BitConverter.ToUInt16(data, 0);        label = "Rücklauf"; value = v.ToString("F0") + " °C";   return true; }
        if (data.Length == 2 && vif == 0x61) { double v = BitConverter.ToUInt16(data, 0) / 100.0;label = "ΔT";       value = v.ToString("F2") + " °C";  return true; }

        return false;
    }

    // --- Datenlänge per DIF ---
    static int DataLengthFromDIF(int code) => code switch
    {
        0x00 => 0, 0x01 => 1, 0x02 => 2, 0x03 => 3, 0x04 => 4,
        0x05 => 4, 0x06 => 6, 0x07 => 8, _ => 0
    };

    // --- Seriennummern-Utils ---
    static byte[] EncodeSerialTypeA(string serial)
    {
        string s = new string((serial ?? "").Where(char.IsDigit).ToArray()).PadLeft(8, '0');
        return new[]
        {
            ToBcd(s[6], s[7]), // LSB-first: Byte0 = Ziffern 7..6
            ToBcd(s[4], s[5]),
            ToBcd(s[2], s[3]),
            ToBcd(s[0], s[1]),
        };
    }
    static byte ToBcd(char tens, char ones) => (byte)(((tens - '0') << 4) | (ones - '0'));
    static string DecodeSerialHuman(byte[] b4)
    {
        var sb = new StringBuilder(8);
        foreach (var b in b4.Reverse())
        {
            sb.Append((b >> 4) & 0xF);
            sb.Append(b & 0xF);
        }
        return sb.ToString();
    }

    // --- ACK-Helfer ---
    static void TryReadAck(SerialPort sp, string label)
    {
        int old = sp.ReadTimeout; 
        sp.ReadTimeout = 300;
        try { 
            int b = sp.ReadByte(); 
            Console.WriteLine(b == 0xE5 ? $"{label}: ACK 0xE5" : $"{label}: RX 0x{b:X2}"); 
        }
        catch (TimeoutException) 
        { 
            Console.WriteLine($"{label}: kein ACK gesehen"); 
        }
        finally { sp.ReadTimeout = old; }
    }
    static bool WaitAck(SerialPort sp, int ms)
    {
        int old = sp.ReadTimeout; sp.ReadTimeout = ms;
        try { return sp.ReadByte() == 0xE5; }
        catch { return false; }
        finally { sp.ReadTimeout = old; }
    }

    // --- Sonstige Helfer ---
    static byte Checksum(IEnumerable<byte> bytes) => (byte)(bytes.Aggregate(0, (acc, x) => acc + x) & 0xFF);

    static Dictionary<string,string> ParseArgs(string[] a)
    {
        var d = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < a.Length; i++)
        {
            var s = a[i];
            if (s.StartsWith("--"))
            {
                var key = s[2..];
                string val = "true";
                if (i + 1 < a.Length && !a[i + 1].StartsWith("--")) val = a[++i];
                d[key] = val;
            }
        }
        return d;
    }
    static int GetInt(Dictionary<string,string> d, string k, int def) => d.TryGetValue(k, out var v) && int.TryParse(v, out var x) ? x : def;
    static bool GetBool(Dictionary<string,string> d, string k, bool def)
        => d.TryGetValue(k, out var v) ? v.Equals("on", StringComparison.OrdinalIgnoreCase) || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1" : def;
}
