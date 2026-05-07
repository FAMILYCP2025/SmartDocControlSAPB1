using SmartDocControl.Application.Models;
using SmartDocControl.Application.Ports;
using SmartDocControl.Domain.Entities;
using SmartDocControl.Domain.Enums;
using SmartDocControl.Infrastructure.ServiceLayer;
using SmartDocControl.Infrastructure.ServiceLayer.Dtos;

namespace SmartDocControl.Infrastructure.Repositories;

internal sealed class ConfigurationRepository : IConfigurationRepository
{
    private readonly ServiceLayerClient _client;

    public ConfigurationRepository(ServiceLayerClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public async Task<IReadOnlyList<DocumentRule>> GetActiveRulesAsync(
        DocumentType? filter = null,
        CancellationToken cancellationToken = default)
    {
        var filterClause = "U_Active eq 'Y'";
        if (filter.HasValue)
        {
            var entitySet = ToEntitySet(filter.Value);
            filterClause += $" and U_EntitySet eq '{entitySet}'";
        }

        var url = $"@JCA_DLC_RULE?$filter={Uri.EscapeDataString(filterClause)}";
        var result = await _client.GetAsync<SapPagedResult<SapRuleDto>>(url, cancellationToken);

        var rules = new List<DocumentRule>(result.Value.Count);
        foreach (var dto in result.Value)
        {
            if (!TryParseDocumentType(dto.EntitySet, out var documentType))
                continue;

            rules.Add(new DocumentRule(
                ruleCode: dto.Code,
                documentType: documentType,
                graceDays: dto.GraceDays,
                isActive: string.Equals(dto.Active, "Y", StringComparison.OrdinalIgnoreCase),
                requireNoTarget: string.Equals(dto.OnlyNoTarget, "Y", StringComparison.OrdinalIgnoreCase),
                requireNoRecentActivity: string.Equals(dto.CheckUpdate, "Y", StringComparison.OrdinalIgnoreCase),
                requireApproved: string.Equals(dto.ReqApproval, "Y", StringComparison.OrdinalIgnoreCase),
                maxDocumentsPerRun: dto.MaxPerRun
            ));
        }

        return rules.AsReadOnly();
    }

    public async Task<IReadOnlyList<Exclusion>> GetExclusionsAsync(
        CancellationToken cancellationToken = default)
    {
        var url = $"@JCA_DLC_EXC?$filter={Uri.EscapeDataString("U_Active eq 'Y'")}";
        var result = await _client.GetAsync<SapPagedResult<SapExclusionDto>>(url, cancellationToken);

        var exclusions = new List<Exclusion>(result.Value.Count);
        foreach (var dto in result.Value)
        {
            if (string.IsNullOrWhiteSpace(dto.ExcType) || string.IsNullOrWhiteSpace(dto.ExcValue))
                continue;

            exclusions.Add(new Exclusion(
                exclusionType: dto.ExcType,
                exclusionValue: dto.ExcValue,
                objType: dto.ObjType,
                reason: dto.Reason
            ));
        }

        return exclusions.AsReadOnly();
    }

    private static string ToEntitySet(DocumentType documentType) => documentType switch
    {
        DocumentType.SalesQuotation => "Quotations",
        DocumentType.SalesOrder => "Orders",
        DocumentType.PurchaseQuotation => "PurchaseQuotations",
        DocumentType.PurchaseOrder => "PurchaseOrders",
        _ => throw new ArgumentOutOfRangeException(nameof(documentType))
    };

    private static bool TryParseDocumentType(string? entitySet, out DocumentType documentType)
    {
        documentType = entitySet switch
        {
            "Quotations" => DocumentType.SalesQuotation,
            "Orders" => DocumentType.SalesOrder,
            "PurchaseQuotations" => DocumentType.PurchaseQuotation,
            "PurchaseOrders" => DocumentType.PurchaseOrder,
            _ => (DocumentType)(-1)
        };
        return Enum.IsDefined(documentType);
    }
}
