using FluentAssertions;
using SheikhTravelSystem.Application.Features.Vehicles;
using SheikhTravelSystem.Application.Features.Vehicles.Commands;

namespace SheikhTravelSystem.Tests.Vehicles;

public class UploadVehicleDocumentCommandValidatorTests
{
    private readonly UploadVehicleDocumentCommandValidator _validator = new();

    private static UploadVehicleDocumentCommand ValidCommand(
        string fileName = "photo.jpg",
        string documentType = "VehicleImage",
        long fileLength = 1024) =>
        new(1, Stream.Null, fileName, "image/jpeg", documentType, null, null, fileLength);

    [Fact]
    public void Validate_FileLengthOver2Mb_ShouldFail()
    {
        var result = _validator.Validate(ValidCommand(fileLength: VehicleUploadLimits.MaxFileBytes + 1));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FileLength");
    }

    [Fact]
    public void Validate_WebpVehicleImage_ShouldPass()
    {
        var result = _validator.Validate(ValidCommand("front.webp", "VehicleImage"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PdfRegistrationDoc_ShouldPass()
    {
        var result = _validator.Validate(ValidCommand("registration.pdf", "RegistrationCertificate"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WrongExtensionForDocType_ShouldFail()
    {
        var result = _validator.Validate(ValidCommand("notes.txt", "InsuranceCertificate"));
        result.IsValid.Should().BeFalse();
    }
}
