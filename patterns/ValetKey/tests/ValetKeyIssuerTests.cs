namespace ValetKey.Tests;

/// <summary>
/// What a valet key guarantees: that a client may reach **one resource, with
/// one permission, for a limited time**, and nothing else.
///
/// Each of those three limits is a separate refusal, and each is tested
/// separately — a key that is scoped but never expires, or expiring but
/// unscoped, is the antipattern this replaces rather than a partial
/// implementation of it.
/// </summary>
public class ValetKeyIssuerTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issues_a_key_scoped_to_one_resource()
    {
        ManualClock clock = new(Noon);
        ValetKeyIssuer issuer = new(clock);

        ValetToken token = issuer.Issue("uploads/invoice-1042.pdf", AccessRight.Write, TimeSpan.FromMinutes(15));

        Assert.Equal("uploads/invoice-1042.pdf", token.Resource);
        Assert.Equal(AccessRight.Write, token.AccessRight);
        Assert.Equal(Noon.AddMinutes(15), token.ExpiresAt);
    }

    [Fact]
    public void Grants_access_to_the_resource_it_names()
    {
        ManualClock clock = new(Noon);
        ValetKeyIssuer issuer = new(clock);
        ResourceStore store = new(issuer);

        ValetToken token = issuer.Issue("uploads/invoice-1042.pdf", AccessRight.Write, TimeSpan.FromMinutes(15));
        store.Write(token, "uploads/invoice-1042.pdf", "PDF-BYTES");

        Assert.Equal("PDF-BYTES", store.ContentOf("uploads/invoice-1042.pdf"));
    }

    [Fact]
    public void Refuses_a_key_presented_for_a_different_resource()
    {
        ManualClock clock = new(Noon);
        ValetKeyIssuer issuer = new(clock);
        ResourceStore store = new(issuer);

        ValetToken token = issuer.Issue("uploads/invoice-1042.pdf", AccessRight.Write, TimeSpan.FromMinutes(15));

        // The key names one blob. Without this check it is a key to the whole
        // container, which is the antipattern the pattern exists to replace.
        Assert.Throws<UnauthorizedAccessException>(() =>
            store.Write(token, "uploads/someone-elses.pdf", "PDF-BYTES"));
    }

    [Fact]
    public void Refuses_a_write_with_a_read_only_key()
    {
        ManualClock clock = new(Noon);
        ValetKeyIssuer issuer = new(clock);
        ResourceStore store = new(issuer);

        ValetToken token = issuer.Issue("reports/q3.pdf", AccessRight.Read, TimeSpan.FromMinutes(15));

        Assert.Throws<UnauthorizedAccessException>(() =>
            store.Write(token, "reports/q3.pdf", "TAMPERED"));
    }

    [Fact]
    public void Refuses_a_key_that_has_expired()
    {
        ManualClock clock = new(Noon);
        ValetKeyIssuer issuer = new(clock);
        ResourceStore store = new(issuer);

        ValetToken token = issuer.Issue("uploads/invoice-1042.pdf", AccessRight.Write, TimeSpan.FromMinutes(15));
        clock.Advance(TimeSpan.FromMinutes(16));

        // The expiry is the only limit that does not need anybody to act. A key
        // without it is a permanent credential handed to a client.
        Assert.Throws<UnauthorizedAccessException>(() =>
            store.Write(token, "uploads/invoice-1042.pdf", "PDF-BYTES"));
    }

    [Fact]
    public void Refuses_a_token_it_never_issued()
    {
        ManualClock clock = new(Noon);
        ValetKeyIssuer issuer = new(clock);
        ResourceStore store = new(issuer);

        ValetToken forged = new("forged-value", "uploads/invoice-1042.pdf", AccessRight.Write, Noon.AddYears(1));

        Assert.Throws<UnauthorizedAccessException>(() =>
            store.Write(forged, "uploads/invoice-1042.pdf", "PDF-BYTES"));
    }
}
