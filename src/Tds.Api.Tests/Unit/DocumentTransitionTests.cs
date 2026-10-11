using Tds.Api.Domain;
using S = Tds.Api.Domain.DocumentStatus;

namespace Tds.Api.Tests.UnitTests;

/// <summary>DOMAIN §7.4 — the transition table, approval conditions and automatic withdrawal.</summary>
public class DocumentTransitionTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);

    private static ControlledDocument Doc(S status, string version = "01")
    {
        var d = new ControlledDocument
        {
            Title = "Upravljanje neusaglašenostima", Code = "PR-08", Kind = DocumentKind.Procedura, DocVersion = version,
            ApprovalDate = Today, FileId = Guid.CreateVersion7(),
        };
        var path = new[] { S.NaPregledu, S.Odobren, S.Povucen };
        foreach (var s in path)
        {
            if (d.Status == status) break;
            d.MoveTo(s, Today);
        }
        Assert.Equal(status, d.Status);
        return d;
    }

    public static TheoryData<S, S> AllowedMoves => new()
    {
        { S.Nacrt, S.NaPregledu }, { S.NaPregledu, S.Nacrt }, { S.NaPregledu, S.Odobren }, { S.Odobren, S.Povucen },
    };

    public static TheoryData<S, S> ForbiddenMoves => new()
    {
        { S.Nacrt, S.Odobren }, { S.Nacrt, S.Povucen }, { S.NaPregledu, S.Povucen },
        { S.Odobren, S.Nacrt }, { S.Odobren, S.NaPregledu },
        { S.Povucen, S.Nacrt }, { S.Povucen, S.NaPregledu }, { S.Povucen, S.Odobren },
    };

    [Theory]
    [MemberData(nameof(AllowedMoves))]
    public void Every_allowed_move_in_the_table(S from, S to)
    {
        var d = Doc(from);
        d.MoveTo(to, Today);
        Assert.Equal(to, d.Status);
    }

    [Theory]
    [MemberData(nameof(ForbiddenMoves))]
    public void Every_move_outside_the_table_is_rejected(S from, S to)
    {
        var d = Doc(from);
        Assert.Throws<DomainException>(() => d.MoveTo(to, Today));
        Assert.Equal(from, d.Status);
    }

    [Fact]
    public void Povucen_is_final_with_a_clear_message()
    {
        var d = Doc(S.Povucen);
        Assert.Equal("Povučen dokument je konačan. Za izmjenu napravi novu verziju.", Assert.Throws<DomainException>(() => d.MoveTo(S.Odobren, Today)).Message);
    }

    [Fact]
    public void Approval_needs_a_date_not_in_the_future_and_a_file()
    {
        var d = Doc(S.NaPregledu);
        d.ApprovalDate = null;
        Assert.Equal("Za odobravanje upiši datum odobrenja.", Assert.Throws<DomainException>(() => d.MoveTo(S.Odobren, Today)).Message);
        d.ApprovalDate = Today.AddDays(1);
        Assert.Equal("Datum odobrenja ne može biti u budućnosti.", Assert.Throws<DomainException>(() => d.MoveTo(S.Odobren, Today)).Message);
        d.ApprovalDate = Today;
        d.FileId = null;
        Assert.Equal("Za odobravanje priloži fajl dokumenta.", Assert.Throws<DomainException>(() => d.MoveTo(S.Odobren, Today)).Message);
        d.FileId = Guid.CreateVersion7();
        d.MoveTo(S.Odobren, Today);
        Assert.Equal(S.Odobren, d.Status);
    }

    [Fact]
    public void Read_only_rules()
    {
        Assert.True(Doc(S.Nacrt).IsEditable);
        Assert.True(Doc(S.NaPregledu).IsEditable);
        Assert.False(Doc(S.Odobren).IsEditable);
        Assert.False(Doc(S.Povucen).IsEditable);
    }

    [Fact]
    public void New_version_copies_data_and_starts_as_draft()
    {
        var v1 = Doc(S.Odobren);
        v1.NextReviewDate = Today.AddYears(1);
        var v2 = v1.NewVersion(" 02 ");
        Assert.Equal((S.Nacrt, "PR-08", "02", v1.Title, v1.FileId), (v2.Status, v2.Code, v2.DocVersion, v2.Title, v2.FileId));
        Assert.Null(v2.ApprovalDate);
        Assert.Null(v2.NextReviewDate);
        Assert.NotEqual(v1.Id, v2.Id);
    }

    [Fact]
    public void Previous_approved_version_of_the_same_code_is_withdrawn()
    {
        var v1 = Doc(S.Odobren);
        var v2 = v1.NewVersion("02");
        v2.ApprovalDate = Today;
        v2.MoveTo(S.NaPregledu, Today);
        v2.MoveTo(S.Odobren, Today);
        v1.WithdrawFor(v2);
        Assert.Equal((S.Povucen, S.Odobren), (v1.Status, v2.Status));

        // Only an approved version of the same code can be withdrawn this way.
        var other = new ControlledDocument { Title = "X", Code = "PR-09", DocVersion = "01" };
        Assert.Throws<InvalidOperationException>(() => Doc(S.Odobren).WithdrawFor(other));
        Assert.Throws<InvalidOperationException>(() => v1.WithdrawFor(v2));   // already Povucen
    }

    [Fact]
    public void Voided_document_cannot_move()
    {
        var d = Doc(S.Nacrt);
        d.Void("Dupli unos");
        Assert.Throws<DomainException>(() => d.MoveTo(S.NaPregledu, Today));
    }
}
