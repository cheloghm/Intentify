using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Intentify.Modules.Promos.Application;
using Intentify.Modules.Promos.Domain;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Promos.Api;

internal static class PromosEndpoints
{
    public static async Task<IResult> CreatePromoAsync(HttpContext context, CreatePromoHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var isMultipart = context.Request.HasFormContentType;

        Guid siteId;
        string name;
        string? description;
        bool isActive;
        byte[]? flyerBytes;
        string? flyerFileName;
        string? flyerContentType;
        IReadOnlyCollection<PromoQuestion>? questions;

        if (isMultipart)
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            if (!Guid.TryParse(form["siteId"], out siteId))
            {
                return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]> { ["siteId"] = ["Site id is invalid."] }));
            }

            name = form["name"].ToString();
            description = string.IsNullOrWhiteSpace(form["description"]) ? null : form["description"].ToString();
            isActive = !bool.TryParse(form["isActive"], out var parsedIsActive) || parsedIsActive;

            questions = ParseQuestions(form["questions"]);
            if (questions is null && !string.IsNullOrWhiteSpace(form["questions"]))
            {
                return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]> { ["questions"] = ["Questions JSON is invalid."] }));
            }

            var flyer = form.Files.GetFile("flyer");
            if (flyer is not null && flyer.Length > 0)
            {
                await using var stream = flyer.OpenReadStream();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, context.RequestAborted);
                flyerBytes = memory.ToArray();
                flyerFileName = flyer.FileName;
                flyerContentType = flyer.ContentType;
            }
            else
            {
                flyerBytes = null;
                flyerFileName = null;
                flyerContentType = null;
            }
        }
        else
        {
            var request = await context.Request.ReadFromJsonAsync<CreatePromoRequest>(context.RequestAborted);
            if (request is null)
            {
                return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = ["Request body is required."] }));
            }

            siteId = request.SiteId;
            name = request.Name;
            description = request.Description;
            isActive = request.IsActive;
            flyerBytes = null;
            flyerFileName = null;
            flyerContentType = null;
            questions = request.Questions?
                .Select(item => new PromoQuestion(item.Key, item.Label, item.Type, item.Required, item.Order))
                .ToArray();
        }

        var result = await handler.HandleAsync(
            new CreatePromoCommand(tenantId.Value, siteId, name, description, isActive, flyerFileName, flyerContentType, flyerBytes, questions),
            context.RequestAborted);

        return result.Status == OperationStatus.ValidationFailed
            ? Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors))
            : Results.Ok(result.Value);
    }

    public static async Task<IResult> ListPromosAsync(HttpContext context, string? siteId, ListPromosHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        Guid? parsedSiteId = null;
        if (!string.IsNullOrWhiteSpace(siteId) && !Guid.TryParse(siteId, out var parsed))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]> { ["siteId"] = ["Site id is invalid."] }));
        }
        else if (!string.IsNullOrWhiteSpace(siteId))
        {
            parsedSiteId = Guid.Parse(siteId!);
        }

        var promos = await handler.HandleAsync(new ListPromosQuery(tenantId.Value, parsedSiteId), context.RequestAborted);
        return Results.Ok(promos);
    }

    public static async Task<IResult> GetPromoDetailAsync(HttpContext context, string promoId, GetPromoDetailHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();
        if (!Guid.TryParse(promoId, out var parsedPromoId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]> { ["promoId"] = ["Promo id is invalid."] }));
        }

        var result = await handler.HandleAsync(new GetPromoDetailQuery(tenantId.Value, parsedPromoId, 1, 200), context.RequestAborted);
        return result.Status == OperationStatus.NotFound ? Results.NotFound() : Results.Ok(result.Value);
    }

    public static async Task<IResult> ListEntriesAsync(HttpContext context, string promoId, int page, int pageSize, ListPromoEntriesHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();
        if (!Guid.TryParse(promoId, out var parsedPromoId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]> { ["promoId"] = ["Promo id is invalid."] }));
        }

        page = page <= 0 ? 1 : page;
        pageSize = pageSize is <= 0 or > 200 ? 50 : pageSize;

        var result = await handler.HandleAsync(new ListPromoEntriesQuery(tenantId.Value, parsedPromoId, page, pageSize), context.RequestAborted);
        return result.Status == OperationStatus.NotFound ? Results.NotFound() : Results.Ok(result.Value);
    }


    public static async Task<IResult> DownloadFlyerAsync(HttpContext context, string promoId, GetPromoDetailHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();
        if (!Guid.TryParse(promoId, out var parsedPromoId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]> { ["promoId"] = ["Promo id is invalid."] }));
        }

        var result = await handler.HandleAsync(new GetPromoDetailQuery(tenantId.Value, parsedPromoId, 1, 1), context.RequestAborted);
        if (result.Status == OperationStatus.NotFound)
        {
            return Results.NotFound();
        }

        var promo = result.Value!.Promo;
        if (promo.FlyerBytes is null || promo.FlyerBytes.Length == 0)
        {
            return Results.NotFound();
        }

        return Results.File(promo.FlyerBytes, promo.FlyerContentType ?? "application/octet-stream", promo.FlyerFileName ?? "flyer");
    }

    public static async Task<IResult> ExportCsvAsync(HttpContext context, string promoId, GetPromoDetailHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();
        if (!Guid.TryParse(promoId, out var parsedPromoId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]> { ["promoId"] = ["Promo id is invalid."] }));
        }

        var result = await handler.HandleAsync(new GetPromoDetailQuery(tenantId.Value, parsedPromoId, 1, 5000), context.RequestAborted);
        if (result.Status == OperationStatus.NotFound)
        {
            return Results.NotFound();
        }

        var detail = result.Value!;
        var answerKeys = detail.Entries
            .SelectMany(item => item.Answers?.Keys ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var fixedColumns = new[] { "id", "promoId", "siteId", "engageSessionId", "visitorId", "firstPartyId", "sessionId", "email", "name", "createdAtUtc" };
        var answerColumns = answerKeys.Select(item => $"q_{item}").ToArray();
        var headers = fixedColumns.Concat(answerColumns).ToArray();

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', headers.Select(EscapeCsv)));

        foreach (var entry in detail.Entries)
        {
            var row = new List<string>
            {
                entry.Id.ToString("N"),
                entry.PromoId.ToString("N"),
                entry.SiteId.ToString("N"),
                entry.EngageSessionId?.ToString("N") ?? string.Empty,
                entry.VisitorId?.ToString("N") ?? string.Empty,
                entry.FirstPartyId ?? string.Empty,
                entry.SessionId ?? string.Empty,
                entry.Email ?? string.Empty,
                entry.Name ?? string.Empty,
                entry.CreatedAtUtc.ToString("O")
            };

            foreach (var key in answerKeys)
            {
                row.Add(entry.Answers is not null && entry.Answers.TryGetValue(key, out var value) ? value : string.Empty);
            }

            builder.AppendLine(string.Join(',', row.Select(EscapeCsv)));
        }

        var fileName = $"promo-{parsedPromoId:N}-entries.csv";
        return Results.File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", fileName);
    }

    public static async Task<IResult> CreatePublicEntryAsync(HttpContext context, string promoKey, CreatePublicPromoEntryRequest request, CreatePublicPromoEntryHandler handler)
    {
        var result = await handler.HandleAsync(
            new CreatePublicPromoEntryCommand(
                promoKey,
                request.VisitorId,
                request.FirstPartyId,
                request.SessionId,
                request.EngageSessionId,
                request.Email,
                request.Name,
                request.ConsentGiven,
                request.ConsentStatement,
                request.Answers),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(result.Value)
        };
    }

    private static IReadOnlyCollection<PromoQuestion>? ParseQuestions(string rawQuestions)
    {
        if (string.IsNullOrWhiteSpace(rawQuestions))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<PromoQuestionRequest[]>(rawQuestions, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return parsed?
                .Select(item => new PromoQuestion(item.Key, item.Label, item.Type, item.Required, item.Order))
                .ToArray();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"', StringComparison.Ordinal))
        {
            value = value.Replace("\"", "\"\"");
        }

        return value.Contains(',', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal)
            ? $"\"{value}\""
            : value;
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantIdValue = user.FindFirstValue("tenantId");
        return Guid.TryParse(tenantIdValue, out var tenantId) ? tenantId : null;
    }
}
