using TTemplate.Api.Infrastructure;
using TTemplate.Api.Settings;

namespace TTemplate.Api.Features.Tickets;

[SettingsSection("tickets", "Заявки", Icon = "system-uicons:flag", Order = 50)]
public sealed class TicketsSettings {
    [Setting("Тека заявок", Description = "Порожньо — <тека даних>/tickets. Відносний шлях — від теки даних. Звідси заявки читає Claude (скіл /tickets)")]
    public string Path { get; set; } = "";
}

/// <summary>
/// Фіча «Заявки»: плаваюча кнопка (Ctrl+Shift+E) → вибір елемента → заявка зі знімком сторінки; сторінка #/tickets — варіанти від Claude,
/// обговорення, погодження. Claude працює з тими самими файлами напряму (скіл .claude/skills/tickets, формат — docs/tickets.md).
/// </summary>
public static class TicketsFeature {
    public static IServiceCollection AddTickets(this IServiceCollection services) {
        services.AddSettingsSection<TicketsSettings>();
        services.AddSingleton<TicketStore>();
        return services;
    }

    public static RouteGroupBuilder MapTickets(this RouteGroupBuilder api) {
        api.MapGet("/tickets", (TicketStore store) => Results.Ok(new { path = store.Root, items = store.List() }));

        api.MapPost("/tickets", (TicketRequest req, TicketStore store) => {
            if (string.IsNullOrWhiteSpace(req.Text)) return ApiErrors.Validation("Опишіть, що не так");
            var id = store.Create(req);
            return Results.Ok(new { id });
        });

        api.MapGet("/tickets/{id}", (string id, TicketStore store) =>
            store.Exists(id) ? Results.Ok(store.Get(id)) : ApiErrors.NotFound($"Заявки {id} немає"));

        // Знімок і варіанти — у sandbox без скриптів (CSP), щоб чужий HTML не виконувався в походженні застосунку.
        api.MapGet("/tickets/{id}/snapshot", (string id, TicketStore store, HttpContext http) =>
            Html(http, store.Exists(id) ? store.Snapshot(id) : null));
        api.MapGet("/tickets/{id}/variants/{slug}", (string id, string slug, TicketStore store, HttpContext http) =>
            Html(http, store.Exists(id) ? store.Variant(id, slug) : null));

        api.MapPost("/tickets/{id}/messages", (string id, CommentRequest body, TicketStore store) => {
            if (!store.Exists(id)) return ApiErrors.NotFound($"Заявки {id} немає");
            if (string.IsNullOrWhiteSpace(body.Text)) return ApiErrors.Validation("Порожній коментар");
            if (body.Variant != null && !TicketStore.ValidSlug(body.Variant)) return ApiErrors.BadRequest("Неправильний варіант");
            store.Comment(id, body.Text, body.Variant);
            return Results.NoContent();
        });

        api.MapPost("/tickets/{id}/action", (string id, ActionRequest body, TicketStore store) => {
            if (!store.Exists(id)) return ApiErrors.NotFound($"Заявки {id} немає");
            var error = store.Action(id, body.Action, body.Variant, body.Text);
            return error == null ? Results.NoContent() : ApiErrors.BadRequest(error);
        });

        api.MapPost("/tickets/{id}/meta", (string id, MetaRequest body, TicketStore store) => {
            if (!store.Exists(id)) return ApiErrors.NotFound($"Заявки {id} немає");
            if (body.Severity is not (null or "none" or "low" or "medium" or "high")) return ApiErrors.BadRequest("Неправильна важливість");
            store.Meta(id, body.Severity, body.Caption);
            return Results.NoContent();
        });
        return api;
    }

    private static IResult Html(HttpContext http, string? html) {
        if (html == null) return ApiErrors.NotFound("Файлу немає");
        http.Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'; style-src 'unsafe-inline'; img-src data: blob:; font-src data:";
        return Results.Content(html, "text/html; charset=utf-8");
    }

    public sealed record CommentRequest(string Text, string? Variant);
    public sealed record ActionRequest(string Action, string? Variant, string? Text);
    public sealed record MetaRequest(string? Severity, string? Caption);
}
