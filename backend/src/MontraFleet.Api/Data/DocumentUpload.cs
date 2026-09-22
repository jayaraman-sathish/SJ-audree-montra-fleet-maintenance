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
                    x.StorageReference, x.UploadedBy, x.UploadedAt, x.ExpiresAt, x.Status,
                    hasFile = x.Content.Length > 0 || db.DocumentAttachments.Any(a => a.VehicleDocumentId == x.Id && a.IsActive),
                    attachments = db.DocumentAttachments.AsNoTracking().Where(a => a.VehicleDocumentId == x.Id && a.IsActive).OrderByDescending(a => a.UploadedAt).Select(a => new { a.Id, a.FileName, a.ContentType, a.UploadedAt, fileUrl = "/api/documents/" + x.Id + "/attachments/" + a.Id + "" }).ToList(),
                    fileUrl = "/api/documents/" + x.Id + "/file"
                })
                .ToListAsync(ct)));

        app.MapPost("/api/documents/upload", async (HttpRequest request, AppDbContext db, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { message = "Upload the document as multipart form data." });

            var form = await request.ReadFormAsync(ct);
            var files = form.Files.GetFiles("files").Concat(form.Files.GetFiles("file")).Where(x => x.Length > 0).ToList();
            if (files.Count == 0)
                return Results.BadRequest(new { message = "Choose a document file." });

            const long maxBytes = 10L * 1024 * 1024;
            if (files.Any(file => file.Length > maxBytes))
                return Results.BadRequest(new { message = "The document must be 10 MB or smaller." });

            if (files.Any(file => !(file.ContentType ?? "").ToLowerInvariant() is "application/pdf" or "image/png" or "image/jpeg"))
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

            var first = files[0];
            await using var stream = first.OpenReadStream(); using var buffer = new MemoryStream(); await stream.CopyToAsync(buffer, ct);

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
                Title = form["title"].FirstOrDefault() ?? Path.GetFileNameWithoutExtension(first.FileName),
                ExpiresAt = DateTime.TryParse(form["expiresAt"].FirstOrDefault(), out var expires) ? expires.ToUniversalTime() : null,
                FileName = Path.GetFileName(first.FileName), ContentType = (first.ContentType ?? "").ToLowerInvariant(),
                Content = buffer.ToArray(),
                StorageReference = "database",
                UploadedBy = form["user"].FirstOrDefault() ?? "Service User",
                UploadedAt = DateTime.UtcNow,
                Status = "Active"
            };

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.VehicleDocuments.Add(document);
            await db.SaveChangesAsync(ct);
            foreach (var attachmentFile in files.Skip(1)) { using var attachmentBuffer = new MemoryStream(); await attachmentFile.CopyToAsync(attachmentBuffer, ct); db.DocumentAttachments.Add(new DocumentAttachment { VehicleDocumentId = document.Id, FileName = Path.GetFileName(attachmentFile.FileName), ContentType = (attachmentFile.ContentType ?? "").ToLowerInvariant(), Content = attachmentBuffer.ToArray(), UploadedBy = document.UploadedBy }); }
            await tx.CommitAsync(ct);

            return Results.Ok(new
            {
                saved = true,
                id = document.Id,
                fileName = document.FileName,
                message = "Document saved successfully."
            });
        });

        app.MapPut("/api/documents/{id:guid}", async (Guid id, HttpRequest request, AppDbContext db, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { message = "Update the document as multipart form data." });

            var document = await db.VehicleDocuments.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (document is null) return Results.NotFound(new { message = "The document was not found." });

            var form = await request.ReadFormAsync(ct);
            var files = form.Files.GetFiles("files").Concat(form.Files.GetFiles("file")).Where(x => x.Length > 0).ToList();
            const long maxBytes = 10L * 1024 * 1024;
            if (files.Count > 0)
            {
                if (files.Any(file => file.Length > maxBytes)) return Results.BadRequest(new { message = "Each document must be 10 MB or smaller." });
                if (files.Any(file => !(file.ContentType ?? "").ToLowerInvariant() is "application/pdf" or "image/png" or "image/jpeg"))
                    return Results.BadRequest(new { message = "Only PDF, PNG and JPEG files are supported." });
                document.Content = Array.Empty<byte>(); document.FileName = ""; document.ContentType = "application/octet-stream";
                var old = await db.DocumentAttachments.Where(a => a.VehicleDocumentId == id && a.IsActive).ToListAsync(ct); foreach (var a in old) a.IsActive = false;
                foreach (var upload in files) { using var buffer = new MemoryStream(); await upload.CopyToAsync(buffer, ct); db.DocumentAttachments.Add(new DocumentAttachment { VehicleDocumentId = id, FileName = Path.GetFileName(upload.FileName), ContentType = (upload.ContentType ?? "").ToLowerInvariant(), Content = buffer.ToArray(), UploadedBy = form["user"].FirstOrDefault() ?? document.UploadedBy }); }
                document.StorageReference = "database";
            }

            var scope = form["documentScope"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(scope) && scope is not ("Customer" or "Manufacturer"))
                return Results.BadRequest(new { message = "Invalid document scope." });
            if (!string.IsNullOrWhiteSpace(scope)) document.DocumentScope = scope;
            if (Guid.TryParse(form["vehicleId"].FirstOrDefault(), out var vehicleId)) document.VehicleId = vehicleId;
            if (Guid.TryParse(form["vehicleModelMasterId"].FirstOrDefault(), out var modelId)) document.VehicleModelMasterId = modelId;
            if (Guid.TryParse(form["jobCardId"].FirstOrDefault(), out var jobCardId)) document.JobCardId = jobCardId;
            if (form.ContainsKey("vehicleId") && string.IsNullOrWhiteSpace(form["vehicleId"])) document.VehicleId = null;
            if (form.ContainsKey("vehicleModelMasterId") && string.IsNullOrWhiteSpace(form["vehicleModelMasterId"])) document.VehicleModelMasterId = null;
            if (form.ContainsKey("jobCardId") && string.IsNullOrWhiteSpace(form["jobCardId"])) document.JobCardId = null;
            if (document.DocumentScope == "Customer" && !document.VehicleId.HasValue)
                return Results.BadRequest(new { message = "Select a vehicle for a customer document." });
            if (document.DocumentScope == "Manufacturer" && (!document.VehicleModelMasterId.HasValue || string.IsNullOrWhiteSpace(document.Title)))
                return Results.BadRequest(new { message = "Select a vehicle model and enter a document title." });
            if (document.VehicleId.HasValue && !await db.Vehicles.AsNoTracking().AnyAsync(x => x.Id == document.VehicleId.Value, ct))
                return Results.BadRequest(new { message = "The selected vehicle was not found." });
            if (document.VehicleModelMasterId.HasValue && !await db.VehicleModelMasters.AsNoTracking().AnyAsync(x => x.Id == document.VehicleModelMasterId.Value && x.IsActive, ct))
                return Results.BadRequest(new { message = "The selected vehicle model was not found." });
            if (document.JobCardId.HasValue && !await db.JobCards.AsNoTracking().AnyAsync(x => x.Id == document.JobCardId.Value, ct))
                return Results.BadRequest(new { message = "The selected Job Card was not found." });
            if (form.ContainsKey("documentType")) document.DocumentType = form["documentType"].FirstOrDefault() ?? document.DocumentType;
            if (form.ContainsKey("manufacturer")) document.Manufacturer = form["manufacturer"].FirstOrDefault() ?? document.Manufacturer;
            if (form.ContainsKey("vehicleType")) document.VehicleType = form["vehicleType"].FirstOrDefault() ?? document.VehicleType;
            if (form.ContainsKey("modelName")) document.ModelName = form["modelName"].FirstOrDefault() ?? document.ModelName;
            if (form.ContainsKey("title")) document.Title = form["title"].FirstOrDefault() ?? document.Title;
            if (form.ContainsKey("revision")) document.Revision = form["revision"].FirstOrDefault() ?? document.Revision;
            if (form.ContainsKey("expiresAt")) document.ExpiresAt = DateTime.TryParse(form["expiresAt"].FirstOrDefault(), out var expires) ? expires.ToUniversalTime() : null;
            document.UploadedBy = form["user"].FirstOrDefault() ?? document.UploadedBy;
            document.UploadedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { saved = true, id = document.Id, fileName = document.FileName, message = "Document updated successfully." });
        });

        app.MapGet("/api/documents/{id:guid}/attachments/{attachmentId:guid}", async (Guid id, Guid attachmentId, AppDbContext db, bool? download) => { var a = await db.DocumentAttachments.AsNoTracking().FirstOrDefaultAsync(x => x.VehicleDocumentId == id && x.Id == attachmentId && x.IsActive); return a is null ? Results.NotFound() : Results.File(a.Content, a.ContentType, download == true ? a.FileName : null); });
    }
}
