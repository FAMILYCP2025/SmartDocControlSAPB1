using SmartDocControl.Application.Models;
using SmartDocControl.Application.Ports;
using SmartDocControl.Domain.Entities;
using SmartDocControl.Domain.Enums;
using SmartDocControl.Infrastructure.ServiceLayer;
using SmartDocControl.Infrastructure.ServiceLayer.Dtos;

namespace SmartDocControl.Infrastructure.Repositories;

internal sealed class DocumentRepository : IDocumentRepository
{
    private const int DefaultMaxDocuments = 500;

    private readonly ServiceLayerClient _client;

    public DocumentRepository(ServiceLayerClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public async Task<IReadOnlyList<Document>> GetOpenDocumentsAsync(
        DocumentRule rule,
        RunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(context);

        var entitySet = ToEntitySet(rule.DocumentType);
        var top = rule.MaxDocumentsPerRun > 0
            ? rule.MaxDocumentsPerRun
            : context.MaxDocumentsPerRun > 0
                ? context.MaxDocumentsPerRun
                : DefaultMaxDocuments;

        const string select = "DocEntry,DocNum,CardCode,CardName,DocDate,DocDueDate,GroupNum,UpdateDate";
        var filter = Uri.EscapeDataString("DocumentStatus eq 'bost_Open'");

        var url = $"{entitySet}?$filter={filter}&$select={select}&$top={top}";

        if (rule.RequireNoTarget)
            url += "&$expand=DocumentLines($select=TargetType,TargetEntry)";

        var result = await _client.GetAsync<SapPagedResult<SapDocumentDto>>(url, cancellationToken);

        var documents = new List<Document>(result.Value.Count);
        foreach (var dto in result.Value)
        {
            var document = MapToDocument(dto, rule.DocumentType);
            if (document is not null)
                documents.Add(document);
        }

        return documents.AsReadOnly();
    }

    private static Document? MapToDocument(SapDocumentDto dto, DocumentType documentType)
    {
        if (dto.DocEntry <= 0 || string.IsNullOrWhiteSpace(dto.CardCode))
            return null;

        if (!DateTime.TryParse(dto.DocDate, out var docDate))
            return null;

        var hasTarget = dto.DocumentLines?.Any(line =>
            (line.TargetType.HasValue && line.TargetType.Value > 0) ||
            (line.TargetEntry.HasValue && line.TargetEntry.Value > 0)) ?? false;

        DateTime? dueDate = dto.DocDueDate is not null && DateTime.TryParse(dto.DocDueDate, out var parsedDue)
            ? parsedDue
            : null;

        return new Document(
            docEntry: dto.DocEntry,
            cardCode: dto.CardCode,
            documentType: documentType,
            baseDate: docDate,          // T1-B: DocDate is BaseDate for MVP
            docNum: dto.DocNum,
            cardName: dto.CardName,
            documentDate: docDate,
            dueDate: dueDate,
            paymentGroupCode: dto.GroupNum,
            hasTargetDocument: hasTarget,
            hasRecentActivity: false,   // T3: pre-computed as false for MVP
            isApproved: false           // D04: not used in MVP
        );
    }

    private static string ToEntitySet(DocumentType documentType) => documentType switch
    {
        DocumentType.SalesQuotation => "Quotations",
        DocumentType.SalesOrder => "Orders",
        DocumentType.PurchaseQuotation => "PurchaseQuotations",
        DocumentType.PurchaseOrder => "PurchaseOrders",
        _ => throw new ArgumentOutOfRangeException(nameof(documentType))
    };
}
