using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Vehicles.Commands;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;

namespace SheikhTravelSystem.API.Models;

public record CreateVehicleApiRequest(CreateVehicleDto Vehicle, bool SaveAsDraft = false);

public record UploadVehicleDocumentForm(
    IFormFile File,
    string DocumentType,
    DateTime? ExpiryDate,
    string? Notes);
