using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TTemplate.Api.Infrastructure;
using TTemplate.Api.Settings;

namespace TTemplate.Api.Features.Tickets;

/// <summary>Що прийшло з діалогу «Заявка» (UI збирає контекст елемента й знімок сторінки).</summary>
public sealed record TicketRequest(
    string Text, string? Severity, string? Scope, string? Url, string? Route, string? Selector, string? Components, string? ComponentFile,
    string? ElementText, string? OuterHtml, string? Viewport, string? Theme, string? UserAgent, string? SnapshotHtml);

public sealed record TicketMessage(string Ts, string Role, string Author, string? Variant, string Text);

/// <summary>
/// Заявки на диску (формат — docs/tickets.md): тека на заявку з ticket.json, snapshot.html, thread.md, events.jsonl, variants/*.html.
/// Усе, крім ticket.json і snapshot.html (пишуться один раз), лише дописується в кінець — ті самі файли паралельно пише Claude.
/// Статус = status останнього рядка events.jsonl з відомим статусом.
/// </summary>
public sealed partial class TicketStore(AppPaths paths, SettingsStore settings) {
    public static readonly string[] Statuses =
        ["new", "awaiting-claude", "variants-ready", "feedback", "approved", "simple", "simple-proposed", "applied", "rejected", "done"];

    // Файли читають людина й Claude: кирилиця без екранування.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly JsonSerializerOptions Line = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly object WriteLock = new();
    private const int SnapshotMaxBytes = 3 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false);

    public string Root {
        get {
            var p = settings.Get<TicketsSettings>().Path;
            var dir = string.IsNullOrWhiteSpace(p) ? Path.Combine(paths.DataDir, "tickets") : Path.GetFullPath(p, paths.DataDir);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private string Author => settings.Get<AppSettings>().UserName;

    public static bool ValidId(string id) => IdRegex().IsMatch(id);
    public static bool ValidSlug(string slug) => SlugRegex().IsMatch(slug);

    private string Dir(string id) => Path.Combine(Root, id);

    public bool Exists(string id) => ValidId(id) && File.Exists(Path.Combine(Dir(id), "ticket.json"));

    // ---------- створення ----------

    public string Create(TicketRequest req) {
        var now = DateTimeOffset.Now;
        var id = $"t-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..4]}";
        var dir = Dir(id);
        Directory.CreateDirectory(dir);

        var ticket = new JsonObject {
            ["id"] = id,
            ["createdAt"] = now.ToString("O"),
            ["author"] = Author,
            ["text"] = req.Text.Trim(),
            ["severity"] = req.Severity,
            ["scope"] = req.Scope,
            ["route"] = req.Route,
            ["url"] = req.Url,
            ["selector"] = req.Selector,
            ["components"] = req.Components,
            ["componentFile"] = req.ComponentFile,
            ["elementText"] = req.ElementText,
            ["outerHtml"] = req.OuterHtml,
            ["viewport"] = req.Viewport,
            ["theme"] = req.Theme,
            ["userAgent"] = req.UserAgent,
        };
        foreach (var key in ticket.Where(p => p.Value == null).Select(p => p.Key).ToList()) ticket.Remove(key);
        File.WriteAllText(Path.Combine(dir, "ticket.json"), ticket.ToJsonString(Json), Utf8);
        if (req.SnapshotHtml is { Length: > 0 and <= SnapshotMaxBytes })
            File.WriteAllText(Path.Combine(dir, "snapshot.html"), req.SnapshotHtml, Utf8);
        AppendEvent(id, new JsonObject { ["status"] = "new", ["by"] = "user", ["author"] = Author, ["scope"] = req.Scope });
        return id;
    }

    // ---------- читання ----------

    public List<object> List() {
        var result = new List<(string CreatedAt, object Item)>();
        foreach (var dir in Directory.EnumerateDirectories(Root)) {
            var id = Path.GetFileName(dir);
            if (!Exists(id)) continue;
            var t = ReadTicket(id);
            var f = Fold(id, t);
            var createdAt = t?["createdAt"]?.GetValue<string>() ?? "";
            result.Add((createdAt, new {
                id, createdAt, f.Status, f.Severity, f.Type, f.Caption,
                text = t?["text"]?.GetValue<string>() ?? "",
                route = t?["route"]?.GetValue<string>(),
                variants = Variants(id, f).Count,
            }));
        }
        return result.OrderByDescending(x => x.CreatedAt, StringComparer.Ordinal).Select(x => x.Item).ToList();
    }

    public object Get(string id) {
        var t = ReadTicket(id);
        var f = Fold(id, t);
        return new {
            id, ticket = t, f.Status, f.Severity, f.Type, f.Caption, history = f.History,
            messages = ReadThread(id),
            variants = Variants(id, f),
            hasSnapshot = File.Exists(Path.Combine(Dir(id), "snapshot.html")),
            dir = Dir(id),
        };
    }

    public string? Snapshot(string id) => ReadIfExists(Path.Combine(Dir(id), "snapshot.html"));

    public string? Variant(string id, string slug) => ValidSlug(slug) ? ReadIfExists(Path.Combine(Dir(id), "variants", slug + ".html")) : null;

    private JsonObject? ReadTicket(string id) {
        try { return JsonNode.Parse(File.ReadAllText(Path.Combine(Dir(id), "ticket.json")))?.AsObject(); } catch { return null; }
    }

    private static string? ReadIfExists(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private sealed record Folded(string Status, string? Severity, string? Type, string? Caption, List<JsonObject> History,
        Dictionary<string, (string Title, string? Summary)> VariantTitles);

    /// <summary>Згортка events.jsonl: статус — з останньої події зі статусом, severity/type/caption — з останнього meta-рядка, де вони є.</summary>
    private Folded Fold(string id, JsonObject? ticket) {
        string status = "new";
        string? severity = ticket?["severity"]?.GetValue<string>(), type = null, caption = null;
        var history = new List<JsonObject>();
        var titles = new Dictionary<string, (string, string?)>();
        foreach (var line in ReadLines(Path.Combine(Dir(id), "events.jsonl"))) {
            JsonObject? e;
            try { e = JsonNode.Parse(line)?.AsObject(); } catch { continue; }
            if (e == null) continue;
            if (e["status"]?.GetValue<string>() is { } s && Statuses.Contains(s)) {
                status = s;
                history.Add(e);
                if (e["variants"] is JsonArray vs)
                    foreach (var v in vs.OfType<JsonObject>())
                        if (v["slug"]?.GetValue<string>() is { } slug)
                            titles[slug] = (v["title"]?.GetValue<string>() ?? slug, v["summary"]?.GetValue<string>());
            } else if (e["kind"]?.GetValue<string>() == "meta") {
                history.Add(e);
                if (e["severity"]?.GetValue<string>() is { } sev) severity = sev == "none" ? null : sev;
                if (e["type"]?.GetValue<string>() is { } tp) type = tp == "none" ? null : tp;
                if (e["caption"]?.GetValue<string>() is { Length: > 0 } cap) caption = cap;
            }
        }
        return new Folded(status, severity, type, caption, history, titles);
    }

    private List<object> Variants(string id, Folded f) {
        var dir = Path.Combine(Dir(id), "variants");
        if (!Directory.Exists(dir)) return [];
        return Directory.EnumerateFiles(dir, "*.html")
            .Select(p => new FileInfo(p))
            .OrderBy(fi => fi.CreationTimeUtc)
            .Select(fi => Path.GetFileNameWithoutExtension(fi.Name))
            .Where(ValidSlug)
            .Select(slug => (object)new {
                slug,
                title = f.VariantTitles.TryGetValue(slug, out var t) ? t.Title : slug,
                summary = f.VariantTitles.TryGetValue(slug, out var t2) ? t2.Summary : null,
            })
            .ToList();
    }

    private List<TicketMessage> ReadThread(string id) {
        var messages = new List<TicketMessage>();
        string? ts = null, role = null, author = null, variant = null;
        var body = new StringBuilder();
        void Flush() {
            if (ts != null) messages.Add(new TicketMessage(ts, role ?? "user", author ?? "", variant, body.ToString().Trim()));
            body.Clear();
        }
        foreach (var line in ReadLines(Path.Combine(Dir(id), "thread.md"), keepEmpty: true)) {
            var m = MessageHeader().Match(line);
            if (m.Success) {
                Flush();
                try {
                    var meta = JsonNode.Parse(m.Groups[1].Value)!.AsObject();
                    ts = meta["ts"]?.GetValue<string>() ?? "";
                    role = meta["role"]?.GetValue<string>();
                    author = meta["author"]?.GetValue<string>();
                    variant = meta["variant"]?.GetValue<string>();
                } catch {
                    ts = null;
                }
                continue;
            }
            if (ts != null) body.Append(line).Append('\n');
        }
        Flush();
        return messages;
    }

    private static IEnumerable<string> ReadLines(string path, bool keepEmpty = false) {
        if (!File.Exists(path)) return [];
        string[] lines;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var r = new StreamReader(fs, Encoding.UTF8))
            lines = r.ReadToEnd().Replace("\r", "").Split('\n');
        return keepEmpty ? lines : lines.Where(l => l.Trim().Length > 0);
    }

    // ---------- дії користувача ----------

    /// <summary>Коментар: new → awaiting-claude; variants-ready / simple-proposed / applied → feedback; інакше статус не змінюється.</summary>
    public void Comment(string id, string text, string? variant) {
        AppendMessage(id, text, variant);
        var status = Fold(id, ReadTicket(id)).Status;
        var next = status switch {
            "new" => "awaiting-claude",
            "variants-ready" or "simple-proposed" or "applied" => "feedback",
            _ => null,
        };
        if (next != null) AppendEvent(id, new JsonObject { ["status"] = next, ["by"] = "user", ["author"] = Author, ["variant"] = variant });
    }

    /// <summary>Кнопки сторінки заявки. Повертає текст помилки або null.</summary>
    public string? Action(string id, string action, string? variant, string? text) {
        var (status, message) = action switch {
            "iterate" => ("feedback", "Ще ітерація"),
            "approve" => ("approved", $"Погоджено варіант `{variant}`"),
            "simple" => ("simple", "Дрібне — просто виправ"),
            "reject" => ("rejected", "Відхилено"),
            "done" => ("done", "Зроблено"),
            _ => (null, null),
        };
        if (status == null) return $"Невідома дія {action}";
        if (action == "approve" && (variant == null || !ValidSlug(variant))) return "Не вказано варіант";
        var full = string.IsNullOrWhiteSpace(text) ? message! : $"{message}\n\n{text.Trim()}";
        AppendMessage(id, full, variant);
        AppendEvent(id, new JsonObject { ["status"] = status, ["by"] = "user", ["author"] = Author, ["variant"] = variant, ["text"] = text });
        return null;
    }

    public void Meta(string id, string? severity, string? caption) {
        var e = new JsonObject { ["kind"] = "meta", ["by"] = "user", ["author"] = Author };
        if (severity != null) e["severity"] = severity;
        if (!string.IsNullOrWhiteSpace(caption)) e["caption"] = caption.Trim().ReplaceLineEndings(" ");
        AppendEvent(id, e);
    }

    // ---------- запис (лише append) ----------

    private void AppendEvent(string id, JsonObject e) {
        var ordered = new JsonObject { ["ts"] = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz") };
        foreach (var (k, v) in e.ToList())
            if (v != null) { e.Remove(k); ordered[k] = v; }
        Append(Path.Combine(Dir(id), "events.jsonl"), ordered.ToJsonString(Line) + "\n");
    }

    private void AppendMessage(string id, string text, string? variant) {
        var path = Path.Combine(Dir(id), "thread.md");
        var now = DateTimeOffset.Now;
        var meta = new JsonObject { ["ts"] = now.ToString("yyyy-MM-ddTHH:mm:sszzz"), ["role"] = "user", ["author"] = Author };
        if (variant != null) meta["variant"] = variant;
        var sb = new StringBuilder();
        if (!File.Exists(path)) sb.Append($"# Обговорення {id}\n");
        sb.Append($"\n### {now:yyyy-MM-dd HH:mm:ss zzz} · user ({Author}){(variant != null ? $" · варіант `{variant}`" : "")} <!-- ticket-msg {meta.ToJsonString(Line)} -->\n\n");
        // Рядок тексту, схожий на заголовок репліки, зсуваємо пробілом — інакше він розірвав би репліку.
        foreach (var line in text.Replace("\r", "").Split('\n'))
            sb.Append(MessageHeader().IsMatch(line) ? " " + line : line).Append('\n');
        Append(path, sb.ToString());
    }

    private static void Append(string path, string text) {
        lock (WriteLock)
            for (var attempt = 0; ; attempt++)
                try {
                    File.AppendAllText(path, text, Utf8);
                    return;
                } catch (IOException) when (attempt < 5) {
                    Thread.Sleep(200); // файл саме дописує Claude
                }
    }

    [GeneratedRegex(@"^t-\d{8}-\d{6}-[0-9a-f]{4}$")]
    private static partial Regex IdRegex();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"^### .* <!-- ticket-msg (\{.*\}) -->\s*$")]
    private static partial Regex MessageHeader();
}
