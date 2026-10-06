
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

static class StaticRuntime
{
    const string RuntimeTag = "ROGIS_RT_";
    const string VisScriptRel = @"script\ROGIS_DepotVisibility.osc";
    const string VisVarsRel = @"script\ROGIS_DepotVisibility_varlist.txt";

    public static void Build(Config cfg, string mapDir, IReadOnlyList<Slot> slots, DepotDay day)
    {
        var byId = slots.ToDictionary(s => s.slotId, StringComparer.OrdinalIgnoreCase);
        CleanupRuntimeObjects(mapDir);

        var staticFolderAbs = Path.Combine(cfg.omsiRoot, cfg.staticObjectFolder.Replace('\\', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(staticFolderAbs);
        Directory.CreateDirectory(Path.Combine(staticFolderAbs, "script"));
        Directory.CreateDirectory(Path.Combine(staticFolderAbs, "script", "runtime"));
        WriteVisibilityScript(staticFolderAbs);

        var additions = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in day.vehicles ?? Array.Empty<DepotVehicle>())
        {
            if (v.vehicleNumber is null) continue;
            var number = v.vehicleNumber.Value;
            var sourceName = string.Format(cfg.staticObjectPattern, number);
            var sourceAbs = Path.Combine(staticFolderAbs, sourceName);
            if (!File.Exists(sourceAbs))
            {
                Console.WriteLine($"STATIC FEHLT: {number} -> {sourceAbs}");
                continue;
            }

            if (!v.used)
            {
                if (v.startSlot is null || !byId.TryGetValue(v.startSlot, out var idleSlot)) continue;
                var path = BuildRuntimeSco(staticFolderAbs, sourceAbs, number, "IDLE", day.date, 0, 0);
                AddObject(additions, idleSlot, RelativeToOmsi(cfg.omsiRoot, path));
                continue;
            }

            if (v.startSlot is not null && byId.TryGetValue(v.startSlot, out var startSlot))
            {
                var dep = ParseClock(v.startTime);
                var path = BuildRuntimeSco(staticFolderAbs, sourceAbs, number, "START", day.date, dep.dayOffset, dep.seconds);
                AddObject(additions, startSlot, RelativeToOmsi(cfg.omsiRoot, path));
            }

            if (v.endSlot is not null && byId.TryGetValue(v.endSlot, out var endSlot))
            {
                var ret = ParseClock(v.endTime);
                var path = BuildRuntimeSco(staticFolderAbs, sourceAbs, number, "END", day.date, ret.dayOffset, ret.seconds);
                AddObject(additions, endSlot, RelativeToOmsi(cfg.omsiRoot, path));
            }
        }

        foreach (var kv in additions)
            AppendObjects(kv.Key, kv.Value);
    }

    static (int dayOffset, int seconds) ParseClock(string? time)
    {
        var m = Regex.Match(time ?? "", @"^(\d{1,2}):(\d{2})$");
        if (!m.Success) return (0, 0);
        var h = int.Parse(m.Groups[1].Value);
        var min = int.Parse(m.Groups[2].Value);
        return (h / 24, (h % 24) * 3600 + min * 60);
    }

    static string BuildRuntimeSco(string folder, string sourceAbs, int number, string mode, string date, int dayOffset, int seconds)
    {
        var src = OmsiText.Read(sourceAbs);
        var runtimeName = $"{RuntimeTag}{number}_{mode}.sco";
        var runtimeAbs = Path.Combine(folder, runtimeName);
        var constRel = $@"script\runtime\{RuntimeTag}{number}_{mode}_const.txt";
        var constAbs = Path.Combine(folder, constRel.Replace('\\', Path.DirectorySeparatorChar));

        var d = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(dayOffset);
        var modeNum = mode == "IDLE" ? 0 : mode == "START" ? 1 : 2;

        var constText = $@"[const]
rogis_mode
{modeNum}

[const]
rogis_year
{d.Year}

[const]
rogis_month
{d.Month}

[const]
rogis_day
{d.Day}

[const]
rogis_time
{seconds}
";
        File.WriteAllText(constAbs, constText, new UTF8Encoding(false));

        src = EnsureListEntry(src, "[script]", VisScriptRel);
        src = EnsureListEntry(src, "[varnamelist]", VisVarsRel);
        src = EnsureListEntry(src, "[constfile]", constRel);
        src = AddRuntimeVisible(src);

        File.WriteAllText(runtimeAbs, src, DetectEncoding(sourceAbs));
        return runtimeAbs;
    }

    static string AddRuntimeVisible(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        for (var i = 0; i < lines.Count - 1; i++)
        {
            if (!lines[i].Trim().Equals("[mesh]", StringComparison.OrdinalIgnoreCase)) continue;
            var insert = i + 2;
            if (insert < lines.Count && lines[insert].Trim().Equals("[visible]", StringComparison.OrdinalIgnoreCase))
                continue;
            lines.Insert(insert, "1");
            lines.Insert(insert, "rogis_visible");
            lines.Insert(insert, "[visible]");
            i += 3;
        }
        return string.Join(Environment.NewLine, lines);
    }

    static string EnsureListEntry(string text, string section, string entry)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        var idx = lines.FindIndex(x => x.Trim().Equals(section, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
        {
            lines.Add("");
            lines.Add(section);
            lines.Add("1");
            lines.Add(entry);
            return string.Join(Environment.NewLine, lines);
        }

        if (idx + 1 >= lines.Count || !int.TryParse(lines[idx + 1].Trim(), out var count))
            throw new Exception($"{section} in SCO ist ungueltig.");

        var entries = lines.Skip(idx + 2).Take(count).Select(x => x.Trim()).ToList();
        if (entries.Any(x => x.Equals(entry, StringComparison.OrdinalIgnoreCase)))
            return string.Join(Environment.NewLine, lines);

        lines[idx + 1] = (count + 1).ToString(CultureInfo.InvariantCulture);
        lines.Insert(idx + 2 + count, entry);
        return string.Join(Environment.NewLine, lines);
    }

    static void WriteVisibilityScript(string folder)
    {
        var script = @"{init}
    0 (S.L.rogis_visible)
{end}

{frame}
    (L.S.Year) (C.L.rogis_year) =
    (L.S.Month) (C.L.rogis_month) = &&
    (L.S.Day) (C.L.rogis_day) = &&
    {if}
        (C.L.rogis_mode) 0 =
        {if}
            1 (S.L.rogis_visible)
        {else}
            (C.L.rogis_mode) 1 =
            {if}
                (L.S.Time) (C.L.rogis_time) < (S.L.rogis_visible)
            {else}
                (L.S.Time) (C.L.rogis_time) >= (S.L.rogis_visible)
            {endif}
        {endif}
    {else}
        0 (S.L.rogis_visible)
    {endif}
{end}
";
        File.WriteAllText(Path.Combine(folder, VisScriptRel.Replace('\\', Path.DirectorySeparatorChar)), script, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(folder, VisVarsRel.Replace('\\', Path.DirectorySeparatorChar)), "rogis_visible" + Environment.NewLine, new UTF8Encoding(false));
    }

    static void AddObject(Dictionary<string, List<string>> additions, Slot slot, string scoPath)
    {
        if (!additions.TryGetValue(slot.mapPath, out var list)) additions[slot.mapPath] = list = new();
        var id = Math.Abs(HashCode.Combine(slot.slotId, scoPath)) + 1000000;
        list.Add($@"
[object]
0
{scoPath}
{id}
{Fmt(slot.x)}
{Fmt(slot.y)}
{Fmt(slot.z)}
{Fmt(slot.heading)}
0
0
");
    }

    static void CleanupRuntimeObjects(string mapDir)
    {
        foreach (var file in Directory.EnumerateFiles(mapDir, "*.map"))
        {
            BackupOnce(file);
            var text = OmsiText.Read(file);
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var output = new List<string>();
            for (var i = 0; i < lines.Length;)
            {
                if (lines[i].Trim().Equals("[object]", StringComparison.OrdinalIgnoreCase) && i + 2 < lines.Length &&
                    lines[i + 2].Contains(RuntimeTag, StringComparison.OrdinalIgnoreCase))
                {
                    i += 3;
                    while (i < lines.Length && !lines[i].TrimStart().StartsWith("["))
                        i++;
                    continue;
                }
                output.Add(lines[i++]);
            }
            var cleaned = string.Join(Environment.NewLine, output);
            if (!string.Equals(text, cleaned, StringComparison.Ordinal))
                OmsiText.WriteLikeOriginal(file, cleaned);
        }
    }

    static void AppendObjects(string mapPath, List<string> blocks)
    {
        BackupOnce(mapPath);
        var text = OmsiText.Read(mapPath).TrimEnd() + Environment.NewLine + string.Join(Environment.NewLine, blocks) + Environment.NewLine;
        OmsiText.WriteLikeOriginal(mapPath, text);
    }

    static void BackupOnce(string path)
    {
        var bak = path + ".rogis.bak";
        if (!File.Exists(bak)) File.Copy(path, bak);
    }

    static string RelativeToOmsi(string root, string full) =>
        Path.GetRelativePath(root, full).Replace('/', '\\');

    static string Fmt(double? d) => (d ?? 0).ToString("0.########", CultureInfo.InvariantCulture);

    static Encoding DetectEncoding(string path)
    {
        var b = File.ReadAllBytes(path);
        return b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE ? Encoding.Unicode : new UTF8Encoding(false);
    }
}
