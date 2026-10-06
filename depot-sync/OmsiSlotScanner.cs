
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

static class OmsiSlotScanner
{
    static readonly Regex MarkerRx = new(@"ROGIS_DepotSlot_(DS|DG|ES|EG)\.sco$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex SlotRx = new(@"^[MSH]\d{3,4}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<Slot> Scan(string mapDir)
    {
        var result = new List<Slot>();
        foreach (var file in Directory.EnumerateFiles(mapDir, "*.map", SearchOption.TopDirectoryOnly))
        {
            var text = OmsiText.Read(file);
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Trim().Equals("[object]", StringComparison.OrdinalIgnoreCase)) continue;
                if (i + 10 >= lines.Length) continue;

                var objectPath = lines[i + 2].Trim();
                var mm = MarkerRx.Match(objectPath.Replace('/', '\\'));
                if (!mm.Success) continue;

                if (!TryNum(lines[i + 4], out var x) ||
                    !TryNum(lines[i + 5], out var y) ||
                    !TryNum(lines[i + 6], out var z) ||
                    !TryNum(lines[i + 7], out var heading)) continue;

                var label = "";
                if (int.TryParse(lines[i + 10].Trim(), out var flags) && (flags & 8) != 0 && i + 11 < lines.Length)
                    label = lines[i + 11].Trim().ToUpperInvariant();

                if (!SlotRx.IsMatch(label))
                {
                    Console.WriteLine($"IGNORIERT: Slotobjekt ohne gueltige Beschriftung in {Path.GetFileName(file)} bei {x:0.0}/{y:0.0}. Erwartet z.B. M037.");
                    continue;
                }

                var depotCode = label[0];
                result.Add(new Slot(
                    label,
                    depotCode == 'S' ? "Betriebshof Spryndorf" : depotCode == 'H' ? "Betriebshof Hechem" : "Betriebshof Mitte",
                    mm.Groups[1].Value.ToUpperInvariant(),
                    Path.GetFileNameWithoutExtension(file),
                    lines[i + 3].Trim(),
                    file,
                    x, y, z, heading
                ));
            }
        }

        var dup = result.GroupBy(s => s.slotId, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).ToList();
        if (dup.Count > 0)
            throw new Exception("Doppelte Stellplatznummer(n): " + string.Join(", ", dup.Select(g => g.Key)));

        return result.OrderBy(s => s.slotId, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static bool TryNum(string raw, out double value) =>
        double.TryParse(raw.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}

static class OmsiText
{
    public static string Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode.GetString(bytes);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(bytes);
        return Encoding.UTF8.GetString(bytes);
    }

    public static void WriteLikeOriginal(string path, string text)
    {
        var bytes = File.ReadAllBytes(path);
        var enc = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE
            ? Encoding.Unicode
            : new UTF8Encoding(false);
        File.WriteAllText(path, text, enc);
    }
}
