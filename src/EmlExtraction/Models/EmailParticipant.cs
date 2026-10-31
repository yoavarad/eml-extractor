namespace EmlExtraction.Models;

/// <summary>One mailbox from a participant header (from, to, cc, bcc, reply-to, sender).</summary>
/// <param name="Nickname">Decoded display name, or <c>""</c> when the mailbox has none.</param>
/// <param name="Address">Mailbox address as parsed; equals <c>User@Domain</c> when it contains <c>@</c>.</param>
/// <param name="User">Part of <paramref name="Address"/> before its last <c>@</c>, or the whole address when it has no <c>@</c>.</param>
/// <param name="Domain">Part of <paramref name="Address"/> after its last <c>@</c>, or <c>""</c> when it has no <c>@</c>.</param>
public sealed record EmailParticipant(string Nickname, string Address, string User, string Domain);
