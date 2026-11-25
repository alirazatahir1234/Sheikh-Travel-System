using System.Text;
using FluentAssertions;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents;
using SheikhTravelSystem.Application.Features.TravelNexus.Passport;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelDocumentRulesTests
{
    private static readonly byte[] Pdf = [.. Encoding.ASCII.GetBytes("%PDF-1.7\n"), 1, 2, 3];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];
    private static readonly byte[] Exe = [0x4D, 0x5A, 0x90, 0x00, 3, 0, 0, 0];

    // ── File validation ────────────────────────────────────────────────────

    [Theory]
    [InlineData("passport.pdf", "application/pdf", ".pdf")]
    [InlineData("photo.JPEG", "image/jpeg", ".jpg")]
    public void Accepts_files_whose_bytes_match_the_extension(string name, string type, string ext)
    {
        var check = TravelDocumentFileValidator.Validate(name, name.EndsWith(".pdf") ? Pdf : Jpeg);
        check.IsValid.Should().BeTrue();
        check.ContentType.Should().Be(type);
        check.Extension.Should().Be(ext);
        check.Sha256.Should().HaveLength(64);
    }

    [Fact]
    public void Rejects_executable_renamed_to_pdf()
        => TravelDocumentFileValidator.Validate("passport.pdf", Exe).Error.Should().Contain("not a valid");

    [Fact]
    public void Rejects_png_bytes_with_jpg_extension()
        => TravelDocumentFileValidator.Validate("photo.jpg", Png).Error.Should().Contain("does not match");

    [Theory]
    [InlineData("passport.exe")]
    [InlineData("passport")]
    [InlineData("passport.pdf.exe")]
    public void Rejects_disallowed_extensions(string name)
        => TravelDocumentFileValidator.Validate(name, Pdf).IsValid.Should().BeFalse();

    [Fact]
    public void Rejects_empty_and_oversized_files()
    {
        TravelDocumentFileValidator.Validate("a.pdf", []).IsValid.Should().BeFalse();
        var big = new byte[TravelDocumentFileValidator.MaxBytes + 1];
        Pdf.CopyTo(big, 0);
        TravelDocumentFileValidator.Validate("a.pdf", big).Error.Should().Contain("10 MB");
    }

    // ── Applicant conditions & planning ────────────────────────────────────

    private static TravelApplicantProfile Person(int id, string gender, int ageAtTravel, int? mahram = null)
        => new(id, gender, new DateTime(2027, 1, 10).AddYears(-ageAtTravel).AddDays(-1), mahram);

    [Theory]
    [InlineData("All", "Male", 30, null, true)]
    [InlineData("Adult", "Male", 18, null, true)]
    [InlineData("Minor", "Male", 17, null, true)]
    [InlineData("Minor", "Female", 18, null, false)]
    [InlineData("Female", "Male", 30, null, false)]
    [InlineData("FemaleWithMahram", "Female", 30, 5, true)]
    [InlineData("FemaleWithMahram", "Female", 30, null, false)]
    public void Conditions_are_evaluated_at_the_travel_date(string condition, string gender, int age, int? mahram, bool applies)
        => TravelChecklistRules.Applies(condition, Person(1, gender, age, mahram), new DateTime(2027, 1, 10)).Should().Be(applies);

    [Fact]
    public void Unknown_date_of_birth_counts_as_adult()
    {
        var p = new TravelApplicantProfile(1, "Male", null, null);
        TravelChecklistRules.Applies("Adult", p, DateTime.UtcNow).Should().BeTrue();
        TravelChecklistRules.Applies("Minor", p, DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void Planner_creates_requirements_per_applicant_and_merges_same_document_type()
    {
        TravelChecklistItemDto Item(int id, int docType, bool mandatory, string condition, int? minDays = null)
            => new() { Id = id, DocumentTypeId = docType, IsMandatory = mandatory, ApplicantCondition = condition, MinValidityDays = minDays, SortOrder = id };

        var items = new[]
        {
            Item(1, 100, true, "All", 180),           // passport
            Item(2, 200, true, "Minor"),              // birth certificate
            Item(3, 300, true, "FemaleWithMahram"),   // mahram proof
            Item(4, 100, false, "Adult", 365),        // passport again for adults, longer validity
        };
        var travel = new DateTime(2027, 1, 10);
        var seeds = TravelChecklistPlanner.Plan(items, [Person(1, "Male", 40), Person(2, "Female", 35, mahram: 1), Person(3, "Male", 8)], travel);

        seeds.Where(s => s.TravelerId == 1).Select(s => s.DocumentTypeId).Should().BeEquivalentTo([100]);
        seeds.Where(s => s.TravelerId == 2).Select(s => s.DocumentTypeId).Should().BeEquivalentTo([100, 300]);
        seeds.Where(s => s.TravelerId == 3).Select(s => s.DocumentTypeId).Should().BeEquivalentTo([100, 200]);
        var adultPassport = seeds.Single(s => s.TravelerId == 1);
        adultPassport.IsMandatory.Should().BeTrue();
        adultPassport.MinValidityDays.Should().Be(365);
    }

    // ── Gate & validity ────────────────────────────────────────────────────

    [Fact]
    public void Gate_counts_verified_and_waived_mandatory_documents_of_active_applicants_only()
    {
        var states = new[]
        {
            new TravelRequirementState(1, true, "Verified"),
            new TravelRequirementState(1, true, "Waived"),
            new TravelRequirementState(1, false, "Missing"),   // optional: ignored
            new TravelRequirementState(2, true, "Uploaded"),
            new TravelRequirementState(9, true, "Missing"),    // inactive applicant: ignored
        };
        var progress = TravelChecklistRules.Progress(states, new HashSet<int> { 1, 2 });
        progress.MandatoryTotal.Should().Be(3);
        progress.MandatoryComplete.Should().Be(2);
        progress.IsComplete.Should().BeFalse();

        TravelChecklistRules.Progress([], new HashSet<int> { 1 }).IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Validity_requires_expiry_beyond_travel_plus_minimum_days()
    {
        var travel = new DateTime(2027, 1, 10);
        TravelChecklistRules.ValidityProblem(null, null, travel).Should().BeNull();
        TravelChecklistRules.ValidityProblem(180, null, travel).Should().Contain("expiry date");
        TravelChecklistRules.ValidityProblem(180, travel.AddDays(179), travel).Should().Contain("Waive");
        TravelChecklistRules.ValidityProblem(180, travel.AddDays(180), travel).Should().BeNull();
    }

    // ── MRZ ────────────────────────────────────────────────────────────────

    private const string IcaoLine1 = "P<UTOERIKSSON<<ANNA<MARIA<<<<<<<<<<<<<<<<<<<";
    private const string IcaoLine2 = "L898902C36UTO7408122F1204159ZE184226B<<<<<10";

    [Fact]
    public void Parses_the_icao_specimen_with_valid_check_digits()
    {
        var mrz = MrzParser.Parse($"REPUBLIC OF UTOPIA\nPASSPORT\n{IcaoLine1}\n{IcaoLine2}\n", new DateTime(2026, 11, 25))!;

        mrz.Surname.Should().Be("ERIKSSON");
        mrz.GivenNames.Should().Be("ANNA MARIA");
        mrz.PassportNumber.Should().Be("L898902C3");
        mrz.DateOfBirth.Should().Be(new DateTime(1974, 8, 12));
        mrz.ExpiryDate.Should().Be(new DateTime(2012, 4, 15));
        mrz.Gender.Should().Be("Female");
        mrz.ChecksumValid.Should().BeTrue();
        mrz.IssuingCountryCode.Should().BeNull("UTO is the ICAO fictional specimen country");
    }

    private static string PakistaniLine2()
    {
        var number = "AB1234567";
        var dob = "900115";
        var exp = "310620";
        var personal = "3520212345671<";
        var line = number + MrzParser.CheckDigit(number) + "PAK" + dob + MrzParser.CheckDigit(dob) + "M" + exp + MrzParser.CheckDigit(exp)
                   + personal + MrzParser.CheckDigit(personal);
        var composite = line[..10] + line[13..20] + line[21..43];
        return line + MrzParser.CheckDigit(composite);
    }

    [Fact]
    public void Maps_alpha3_country_codes_and_handles_ocr_noise()
    {
        var line2 = PakistaniLine2();
        // OCR noise: spaces, « instead of <, letter O for zero in the date of birth.
        var noisy = "P<PAKKHAN<<MUHAMMAD«ALI<<<<<<<<<<<<<<<<<<<<<<<<\n" + line2[..13] + "9O" + line2[15..].Insert(10, " ");
        var mrz = MrzParser.Parse(noisy, new DateTime(2026, 11, 25))!;

        mrz.Surname.Should().Be("KHAN");
        mrz.GivenNames.Should().Be("MUHAMMAD ALI");
        mrz.IssuingCountryCode.Should().Be("PK");
        mrz.NationalityCode.Should().Be("PK");
        mrz.DateOfBirth.Should().Be(new DateTime(1990, 1, 15));
        mrz.ExpiryDate.Should().Be(new DateTime(2031, 6, 20));
        mrz.ChecksumValid.Should().BeTrue();
    }

    [Fact]
    public void Flags_bad_check_digits()
    {
        var line2 = PakistaniLine2();
        var tampered = line2[..2] + (line2[2] == '1' ? '2' : '1') + line2[3..]; // change one passport-number character
        var mrz = MrzParser.Parse("P<PAKKHAN<<ALI<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<\n" + tampered)!;

        mrz.ChecksumValid.Should().BeFalse();
        mrz.InvalidFields.Should().Contain(["PassportNumber", "Composite"]);
    }

    [Fact]
    public void Returns_null_when_no_mrz_is_present()
    {
        MrzParser.Parse(null).Should().BeNull();
        MrzParser.Parse("ISLAMIC REPUBLIC OF PAKISTAN\nPASSPORT\nName: Ali").Should().BeNull();
    }
}
