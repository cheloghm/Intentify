using Intentify.Modules.Sites.Application;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application;

public sealed class WidgetBootstrapHandler
{
    private readonly ISiteRepository _siteRepository;

    public WidgetBootstrapHandler(ISiteRepository siteRepository)
    {
        _siteRepository = siteRepository;
    }

    public async Task<OperationResult<WidgetBootstrapResult>> HandleAsync(WidgetBootstrapQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.WidgetKey))
        {
            var validationErrors = new ValidationErrors();
            validationErrors.Add("widgetKey", "Widget key is required.");

            return OperationResult<WidgetBootstrapResult>.ValidationFailed(validationErrors);
        }

        var site = await _siteRepository.GetByWidgetKeyAsync(query.WidgetKey, cancellationToken);
        if (site is null)
        {
            return OperationResult<WidgetBootstrapResult>.NotFound();
        }

        return OperationResult<WidgetBootstrapResult>.Success(new WidgetBootstrapResult(site.Id, site.Domain));
    }
}
