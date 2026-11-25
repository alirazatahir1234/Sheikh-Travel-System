namespace SheikhTravelSystem.Application.Features.TravelNexus.Documents;

/// <summary>Traveler facts needed to decide which checklist items apply.</summary>
public sealed record TravelApplicantProfile(int TravelerId, string Gender, DateTime? DateOfBirth, int? MahramTravelerId);

/// <summary>Requirement status facts for the verify gate.</summary>
public sealed record TravelRequirementState(int ApplicantId, bool IsMandatory, string Status);

public sealed record TravelChecklistProgress(int MandatoryTotal, int MandatoryComplete)
{
    public int MandatoryOutstanding => MandatoryTotal - MandatoryComplete;
    public bool IsComplete => MandatoryComplete == MandatoryTotal;
}

/// <summary>Pure checklist rules: applicant conditions and the "documents verified" gate.</summary>
public static class TravelChecklistRules
{
    public const int AdultAge = 18;

    /// <summary>
    /// Whether an item with <paramref name="condition"/> applies to the applicant. Age is measured at the intended
    /// travel date (or today). An unknown date of birth counts as adult.
    /// </summary>
    public static bool Applies(string condition, TravelApplicantProfile applicant, DateTime referenceDate)
    {
        var minor = applicant.DateOfBirth is DateTime dob && AgeOn(dob, referenceDate) < AdultAge;
        return condition switch
        {
            "All" => true,
            "Adult" => !minor,
            "Minor" => minor,
            "Male" => applicant.Gender == "Male",
            "Female" => applicant.Gender == "Female",
            "FemaleWithMahram" => applicant.Gender == "Female" && applicant.MahramTravelerId.HasValue,
            _ => false
        };
    }

    public static int AgeOn(DateTime dateOfBirth, DateTime on)
    {
        var age = on.Year - dateOfBirth.Year;
        if (on.Date < dateOfBirth.Date.AddYears(age)) age--;
        return age;
    }

    /// <summary>Mandatory requirements of active applicants count as complete when Verified or Waived.</summary>
    public static TravelChecklistProgress Progress(IEnumerable<TravelRequirementState> requirements, IReadOnlySet<int> activeApplicantIds)
    {
        var mandatory = requirements.Where(r => r.IsMandatory && activeApplicantIds.Contains(r.ApplicantId)).ToList();
        return new TravelChecklistProgress(mandatory.Count, mandatory.Count(r => r.Status is "Verified" or "Waived"));
    }

    /// <summary>
    /// Null when the document is valid long enough after travel; otherwise a user-facing reason.
    /// </summary>
    public static string? ValidityProblem(int? minValidityDays, DateTime? documentExpiry, DateTime referenceDate)
    {
        if (minValidityDays is not int days) return null;
        if (documentExpiry is null) return $"Enter the document's expiry date (it must be valid {days} days after travel).";
        var required = referenceDate.Date.AddDays(days);
        return documentExpiry.Value.Date < required
            ? $"The document expires {documentExpiry.Value:dd MMM yyyy}; it must be valid until at least {required:dd MMM yyyy}. Waive with a reason to override."
            : null;
    }
}
