using SmartDocControl.Domain.Enums;

namespace SmartDocControl.Domain.Entities;

public sealed class Document
{
    public int DocEntry { get; }
    public int DocNum { get; }
    public string CardCode { get; }
    public string CardName { get; }
    public DocumentType DocumentType { get; }
    public DateTime BaseDate { get; }
    public DateTime? DocumentDate { get; }
    public DateTime? DueDate { get; }
    public int? PaymentGroupCode { get; }
    public bool HasTargetDocument { get; }
    public bool HasRecentActivity { get; }
    public bool IsApproved { get; }
    public bool IsClosed { get; private set; }

    public Document(
        int docEntry,
        string cardCode,
        DocumentType documentType,
        DateTime baseDate,
        int docNum = 0,
        string? cardName = null,
        DateTime? documentDate = null,
        DateTime? dueDate = null,
        int? paymentGroupCode = null,
        bool hasTargetDocument = false,
        bool hasRecentActivity = false,
        bool isApproved = false,
        bool isClosed = false)
    {
        if (docEntry <= 0)
            throw new ArgumentOutOfRangeException(nameof(docEntry), "DocEntry must be greater than zero.");
        if (string.IsNullOrWhiteSpace(cardCode))
            throw new ArgumentException("CardCode is required.", nameof(cardCode));
        if (baseDate == default)
            throw new ArgumentException("BaseDate is required.", nameof(baseDate));
        if (!Enum.IsDefined(documentType))
            throw new ArgumentOutOfRangeException(nameof(documentType), "Invalid document type.");

        DocEntry = docEntry;
        DocNum = docNum;
        CardCode = cardCode;
        CardName = cardName ?? string.Empty;
        DocumentType = documentType;
        BaseDate = baseDate;
        DocumentDate = documentDate;
        DueDate = dueDate;
        PaymentGroupCode = paymentGroupCode;
        HasTargetDocument = hasTargetDocument;
        HasRecentActivity = hasRecentActivity;
        IsApproved = isApproved;
        IsClosed = isClosed;
    }

    public bool CanBeClosed() => !IsClosed;

    public void MarkAsClosed() => IsClosed = true;
}
