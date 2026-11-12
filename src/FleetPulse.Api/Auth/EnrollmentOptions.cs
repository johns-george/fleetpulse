namespace FleetPulse.Api.Auth;

public class EnrollmentOptions
{
    public const string SectionName = "Enrollment";

    /// <summary>Demo-only shared secret a device must present to enroll.</summary>
    public string Key { get; set; } = string.Empty;
}
