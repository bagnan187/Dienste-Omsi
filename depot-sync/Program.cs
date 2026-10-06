using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

record Config(string apiBaseUrl, string depotSyncToken, string omsiRoot, string mapFolder, int udpPort = 47830, int pollSeconds = 60);
record Slot(string slotId, string depot, string slotType, string tile, string objectId, string mapPath, double? x, double? y, double? z, double? heading);
record Marker(string DepotCode, string SlotType, string Tile, string ObjectPath, string MapPath, double X, double Y, double Z, double Heading);

static class Program
{
    static readonly Regex MarkerRx = new(@"ROGIS_DepotSlot_([MSH])_(DS|DG|ES|EG)\.sco", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    static async Task Main()
    {
        var baseDir = AppContext.BaseDirectory;
        var cfgPath = Path.Combine(baseDir, "config.json");
        if (!File.Exists(cfgPath))
        {
            Console.WriteLine("config.json fehlt. config.example.json kopieren und anpassen.");
            return;
        }

        var cfg = JsonSerializer.Deserialize<Config>(await File.ReadAllTextAsync(cfgPath), Json)!;
        var mapDir = Path.Combine(cfg.omsiRoot, "maps", cfg.mapFolder);
        if (!Directory.Exists(mapDir))
        {
            Console.WriteLine($"Map-Ordner nicht gefunden: {mapDir}");
            return;
        }

        using var http = new HttpClient { BaseAddress = new Uri(cfg.apiBaseUrl.TrimEnd('/') + "/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cfg.depotSyncToken);

        var udp = new UdpClient(cfg.udpPort);
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var r = await udp.ReceiveAsync();
                    var s = Encoding.UTF8.GetString(r.Buffer);
                    Console.WriteLine($"openOMSI: {s}");
                    await SyncAll(cfg, mapDir, http);
                }
                catch (Exception ex) { Console.WriteLine("UDP: " + ex.Message); }
            }
        });

        while (true)
        {
            try { await SyncAll(cfg, mapDir, http); }
            catch (Exception ex) { Console.WriteLine("Sync: " + ex.Message); }
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(15, cfg.pollSeconds)));
        }
    }

    static async Task SyncAll(Config cfg, string mapDir, HttpClient http)
    {
        var markers = ScanMarkers(mapDir);
        var slots = AssignStableIds(markers, Path.Combine(AppContext.BaseDirectory, "slot-registry.json"));

        var payload = JsonSerializer.Serialize(new { source = "ROGIS Depot Sync", slots }, Json);
        using var post = new StringContent(payload, Encoding.UTF8, "application/json");
        var resp = await http.PostAsync("api/openomsi/depot-slots", post);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) throw new Exception($"Slot-Upload HTTP {(int)resp.StatusCode}: {body}");

        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var day = await http.GetStringAsync($"api/openomsi/depot-day?date={date}");
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "depot-day.json"), day, Encoding.UTF8);
        Console.WriteLine($"{DateTime.Now:HH:mm:ss}: {slots.Count} Slots synchronisiert, Tagesbelegung aktualisiert.");
    }

    static List<Marker> ScanMarkers(string mapDir)
    {
        var result = new List<Marker>();
        foreach (var file in Directory.EnumerateFiles(mapDir, "*.map", SearchOption.TopDirectoryOnly))
        {
            string text = ReadOmsiText(file);
            var lines = text.Replace("\r\n", "\n").Replace('\r','\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var m = MarkerRx.Match(lines[i]);
                if (!m.Success) continue;

                var nums = new List<double>();
                for (int j = i + 1; j < Math.Min(lines.Length, i + 16) && nums.Count < 4; j++)
                {
                    var raw = lines[j].Trim().Replace(',', '.');
                    if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n))
                        nums.Add(n);
                    else if (raw.StartsWith("[") && nums.Count > 0) break;
                }
                if (nums.Count < 4) continue;

                result.Add(new Marker(
                    m.Groups[1].Value.ToUpperInvariant(),
                    m.Groups[2].Value.ToUpperInvariant(),
                    Path.GetFileNameWithoutExtension(file),
                    lines[i].Trim(),
                    file,
                    nums[0], nums[1], nums[2], nums[3]));
            }
        }
        return result;
    }

    static string ReadOmsiText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode.GetString(bytes);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(bytes);
        try { return Encoding.UTF8.GetString(bytes); } catch { return Encoding.GetEncoding(1252).GetString(bytes); }
    }

    static List<Slot> AssignStableIds(List<Marker> markers, string registryPath)
    {
        var old = new Dictionary<string,string>();
        if (File.Exists(registryPath))
        {
            try { old = JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(registryPath)) ?? new(); } catch {}
        }

        string Key(Marker m) => $"{m.DepotCode}|{m.SlotType}|{m.Tile}|{Math.Round(m.X,1)}|{Math.Round(m.Y,1)}|{Math.Round(m.Heading,1)}";
        var used = new HashSet<string>(old.Values, StringComparer.OrdinalIgnoreCase);
        var next = new Dictionary<string,int> { ["M"]=1, ["S"]=1, ["H"]=1 };
        foreach (var id in used)
        {
            var mm = Regex.Match(id, @"^([MSH])(\d+)$", RegexOptions.IgnoreCase);
            if (mm.Success) next[mm.Groups[1].Value.ToUpperInvariant()] = Math.Max(next[mm.Groups[1].Value.ToUpperInvariant()], int.Parse(mm.Groups[2].Value)+1);
        }

        var slots = new List<Slot>();
        foreach (var m in markers.OrderBy(x=>x.DepotCode).ThenBy(x=>x.Tile).ThenBy(x=>x.X).ThenBy(x=>x.Y))
        {
            var key = Key(m);
            if (!old.TryGetValue(key, out var id))
            {
                id = $"{m.DepotCode}{next[m.DepotCode]++:000}";
                old[key] = id;
            }
            slots.Add(new Slot(id, DepotName(m.DepotCode), m.SlotType, m.Tile, key, m.MapPath, m.X, m.Y, m.Z, m.Heading));
        }

        File.WriteAllText(registryPath, JsonSerializer.Serialize(old, Json));
        return slots;
    }

    static string DepotName(string c) => c switch
    {
        "S" => "Betriebshof Spryndorf",
        "H" => "Betriebshof Hechem",
        _ => "Betriebshof Mitte"
    };
}
