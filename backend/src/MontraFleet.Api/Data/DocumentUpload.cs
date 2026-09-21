using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Models;

namespace MontraFleet.Api.Data;

public static class DocumentUpload
{
    public static void MapDocumentUpload(this WebApplication app)
    {
        app.MapGet("/api/documents", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.VehicleDocuments.AsNoTracking()
                .OrderByDescending(x => x.UploadedAt)
                .Select(x => new
                {
                    x.Id, x.VehicleId, x.VehicleModelMasterId, x.JobCardId,
                    x.DocumentScope, x.DocumentType, x.FileName, x.Title,
                    x.Manufacturer, x.VehicleType, x.ModelName, x.Revision,
                    x.UploadedBy, x.UploadedAt, x.ExpiresAt, x.Status,
                    hasFile = x.Content.Length > 0
                })
                .ToListAsync(ct)));

    public static void MapDocumentUpload(this WebApplication app)
    {
        app.MapPost("/api/documents/upload", async (HttpRequest request, AppDbContext db, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { message = "Upload the document as multipart form data." });

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { message = "Choose a document file." });

            const long maxBytes = 10L * 1024 * 1024;
            if (file.Length > maxBytes)
                return Results.BadRequest(new { message = "The document must be 10 MB or smaller." });

            var contentType = (file.ContentType ?? "").ToLowerInvariant();
            var allowed = contentType is "application/pdf" or "image/png" or "image/jpeg";
            if (!allowed)
                return Results.BadRequest(new { message = "Only PDF, PNG and JPEG files are supported." });

            var scope = form["documentScope"].FirstOrDefault() ?? "Customer";
            if (scope is not ("Customer" or "Manufacturer"))
                return Results.BadRequest(new { message = "Invalid document scope." });

            Guid? vehicleId = Guid.TryParse(form["vehicleId"].FirstOrDefault(), out var vehicle) ? vehicle : null;
            Guid? modelId = Guid.TryParse(form["vehicleModelMasterId"].FirstOrDefault(), out var model) ? model : null;
            Guid? jobCardId = Guid.TryParse(form["jobCardId"].FirstOrDefault(), out var job) ? job : null;

            if (scope == "Customer" && !vehicleId.HasValue)
                return Results.BadRequest(new { message = "Select a vehicle for a customer document." });
            if (scope == "Manufacturer" && (!modelId.HasValue || string.IsNullOrWhiteSpace(form["title"].FirstOrDefault())))
                return Results.BadRequest(new { message = "Select a vehicle model and enter a document title." });

            if (vehicleId.HasValue && !await db.Vehicles.AsNoTracking().AnyAsync(x => x.Id == vehicleId.Value, ct))
                return Results.BadRequest(new { message = "The selected vehicle was not found." });
            if (modelId.HasValue && !await db.VehicleModelMasters.AsNoTracking().AnyAsync(x => x.Id == modelId.Value && x.IsActive, ct))
                return Results.BadRequest(new { message = "The selected vehicle model was not found." });
            if (jobCardId.HasValue && !await db.JobCards.AsNoTracking().AnyAsync(x => x.Id == jobCardId.Value, ct))
                return Results.BadRequest(new { message = "The selected Job Card was not found." });

            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);

            var document = new VehicleDocument
            {
                VehicleId = vehicleId,
                VehicleModelMasterId = modelId,
                JobCardId = jobCardId,
                DocumentScope = scope,
                DocumentType = form["documentType"].FirstOrDefault() ?? "Other",
                Manufacturer = form["manufacturer"].FirstOrDefault() ?? "",
                VehicleType = form["vehicleType"].FirstOrDefault() ?? "",
                ModelName = form["modelName"].FirstOrDefault() ?? "",
                Revision = form["revision"].FirstOrDefault() ?? "",
                Title = form["title"].FirstOrDefault() ?? Path.GetFileNameWithoutExtension(file.FileName),
                ExpiresAt = DateTime.TryParse(form["expiresAt"].FirstOrDefault(), out var expires) ? expires.ToUniversalTime() : null,
                FileName = Path.GetFileName(file.FileName),
                ContentType = contentType,
                Content = buffer.ToArray(),
                StorageReference = "database",
                UploadedBy = form["user"].FirstOrDefault() ?? "Service User",
                UploadedAt = DateTime.UtcNow,
                Status = "Active"
            };

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.VehicleDocuments.Add(document);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Results.Ok(new
            {
                saved = true,
                id = document.Id,
                fileName = document.FileName,
                message = "Document saved successfully."
            });
        });
    }
}
