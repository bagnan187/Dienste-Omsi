using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

static class Program
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

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
                    Console.WriteLine("openOMSI: " + Encoding.UTF8.GetString(r.Buffer));
                    await SyncAll(cfg, mapDir, http, buildRuntime: false);
                }
                catch (Exception ex) { Console.WriteLine("UDP: " + ex.Message); }
            }
        });

        var runtimeBuilt = false;
        while (true)
        {
            try
            {
                runtimeBuilt = await SyncAll(cfg, mapDir, http, buildRuntime: !runtimeBuilt) || runtimeBuilt;
            }
            catch (Exception ex) { Console.WriteLine("Sync: " + ex.Message); }
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(15, cfg.pollSeconds)));
        }
    }

    static async Task<bool> SyncAll(Config cfg, string mapDir, HttpClient http, bool buildRuntime = false)
    {
        var slots = OmsiSlotScanner.Scan(mapDir);

        var payload = JsonSerializer.Serialize(new { source = "ROGIS Live Client / Depot Sync", slots }, Json);
        using (var post = new StringContent(payload, Encoding.UTF8, "application/json"))
        {
            var resp = await http.PostAsync("api/openomsi/depot-slots", post);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Slot-Upload HTTP {(int)resp.StatusCode}: {body}");
        }

        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var dayJson = await http.GetStringAsync($"api/openomsi/depot-day?date={date}");
        var day = JsonSerializer.Deserialize<DepotDay>(dayJson, Json)
                  ?? throw new Exception("Depot-Tagesbelegung konnte nicht gelesen werden.");

        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "depot-day.json"), dayJson, Encoding.UTF8);

        if (buildRuntime)
        {
            StaticRuntime.Build(cfg, mapDir, slots, day);
            Console.WriteLine("Static-Hofbelegung fuer diesen OMSI-Start erzeugt.");
        }

        Console.WriteLine($"{DateTime.Now:HH:mm:ss}: {slots.Count} Stellplaetze synchronisiert, {day.vehicles.Length} Fahrzeuge verarbeitet.");
        return buildRuntime;
    }
}
