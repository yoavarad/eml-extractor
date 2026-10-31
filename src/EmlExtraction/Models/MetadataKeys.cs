namespace EmlExtraction.Models;

/// <summary>Well-known keys of the metadata dictionary on an extraction result.</summary>
public static class MetadataKeys
{
    /// <summary>Sender mailbox list (<c>From</c> header).</summary>
    public const string From = "from";

    /// <summary>Primary recipients (<c>To</c> header).</summary>
    public const string To = "to";

    /// <summary>Carbon-copy recipients (<c>Cc</c> header).</summary>
    public const string Cc = "cc";

    /// <summary>Blind-carbon-copy recipients (<c>Bcc</c> header).</summary>
    public const string Bcc = "bcc";

    /// <summary>Reply-to mailbox list (<c>Reply-To</c> header).</summary>
    public const string ReplyTo = "reply-to";

    /// <summary>Actual sender mailbox (<c>Sender</c> header).</summary>
    public const string Sender = "sender";

    /// <summary>Message subject (<c>Subject</c> header).</summary>
    public const string Subject = "subject";

    /// <summary>Message date, round-trip ISO 8601 (<c>Date</c> header).</summary>
    public const string Date = "date";

    /// <summary>Message identifier (<c>Message-Id</c> header).</summary>
    public const string MessageId = "message-id";

    /// <summary>Identifier of the message replied to (<c>In-Reply-To</c> header).</summary>
    public const string InReplyTo = "in-reply-to";

    /// <summary>Space-separated referenced message identifiers (<c>References</c> header).</summary>
    public const string References = "references";

    /// <summary>All <c>X_</c> headers, one <c>Name: value</c> line each.</summary>
    public const string XFields = "xfields";
}
