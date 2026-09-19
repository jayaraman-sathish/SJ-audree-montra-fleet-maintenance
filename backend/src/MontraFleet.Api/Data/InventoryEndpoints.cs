using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Models;
namespace MontraFleet.Api.Data;
public static class InventoryEndpoints
{
 public record Movement(Guid PartMasterId,Guid InventoryLocationId,decimal Quantity,string User,string Reference="",Guid? OperationId=null);
 public record ActionInput(decimal Quantity,string User,string Reference="",Guid? OperationId=null);
 static IResult Bad(string text)=>Results.BadRequest(new{message=text});
 public static void MapInventoryEndpoints(this WebApplication app)
 {
  foreach(var action in new[]{"reserve","issue","return","consume","cancel"}){
   var verb=action;app.MapPost("/api/part-requests/{id:guid}/"+verb,async(Guid id,ActionInput input,AppDbContext db)=>{
    if(string.IsNullOrWhiteSpace(input.User))return Bad("Enter the operator name.");
    await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    var r=await db.PartRequests.FindAsync(id);if(r==null)return Results.NotFound();
    if(verb=="cancel"&&string.IsNullOrWhiteSpace(input.Reference))return Bad("Enter the reason for closing the remaining request.");
    if(input.OperationId.HasValue&&await db.PartTransactions.AnyAsync(x=>x.Id==input.OperationId.Value))return Results.Conflict(new{message="This operation was already posted. Refresh the records."});
    var e=await(from j in db.JobCards join s in db.ServiceEvents on j.ServiceEventId equals s.Id where j.Id==r.JobCardId select s).FirstOrDefaultAsync();
    if(e==null||e.Status=="Closed"||e.Status=="Cancelled")return Bad("Inventory cannot be posted to a closed/cancelled service.");
    var p=await db.PartMasters.FindAsync(r.PartMasterId);if(p==null)return Bad("Part master missing.");
    var stock=await db.PartStocks.SingleOrDefaultAsync(x=>x.PartMasterId==r.PartMasterId&&x.InventoryLocationId==r.InventoryLocationId);if(stock==null)return Bad("Receive stock first.");
    if(verb=="reserve"||verb=="issue"){
     var ledger=await db.PartTransactions.Where(x=>x.PartMasterId==r.PartMasterId&&x.InventoryLocationId==r.InventoryLocationId).ToListAsync();
     if(ledger.Sum(InventoryRules.StockDelta)!=stock.OnHandQty)return Results.Conflict(new{message="Stock does not tally with the ledger. Record a verified physical count in Reconciliation before reserving or issuing."});
    }
    decimal qty;try{qty=InventoryRules.Apply(stock,r,verb,input.Quantity);}catch(InvalidOperationException ex){return Results.Conflict(new{message=ex.Message});}
    var issues=await db.PartTransactions.Where(x=>x.PartRequestId==id&&x.TransactionType=="Issue").ToListAsync();
    var cost=(verb=="return"||verb=="consume")&&issues.Sum(x=>x.Quantity)>0?issues.Sum(x=>x.ExtendedCost)/issues.Sum(x=>x.Quantity):p.StandardCost;
    var kind=verb=="cancel"?"Unreserve":char.ToUpperInvariant(verb[0])+verb[1..];
    db.PartTransactions.Add(new PartTransaction{Id=input.OperationId??Guid.NewGuid(),PartRequestId=r.Id,PartMasterId=p.Id,InventoryLocationId=r.InventoryLocationId,JobCardId=r.JobCardId,WorkItemId=r.WorkItemId,PartNumber=p.PartNumber,PartDescription=p.Description,TransactionType=kind,Quantity=qty,UnitCost=cost,ExtendedCost=verb=="issue"?Math.Round(qty*cost,2):verb=="return"?-Math.Round(qty*cost,2):0,PerformedBy=input.User,Reference=input.Reference});
    ControlEndpoints.Log(db,verb.ToUpperInvariant(),"PartRequest",id,$"{p.PartNumber}: {qty}; {input.Reference}",input.User);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(r);
   });
  }
  foreach(var kind in new[]{"receive","count"}){
   var operation=kind;app.MapPost("/api/parts/"+kind,async(Movement input,AppDbContext db)=>{
    if(string.IsNullOrWhiteSpace(input.User)||string.IsNullOrWhiteSpace(input.Reference)||input.Quantity<0||(operation=="receive"&&input.Quantity==0))return Bad("Enter quantity, operator and receipt/count reference.");
    await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    if(input.OperationId.HasValue&&await db.PartTransactions.AnyAsync(x=>x.Id==input.OperationId))return Results.Conflict(new{message="Operation already posted. Refresh records."});
    var p=await db.PartMasters.FindAsync(input.PartMasterId);var l=await db.InventoryLocations.FindAsync(input.InventoryLocationId);if(p==null||l==null||!p.IsActive||!l.IsActive)return Bad("Select an active part and location.");
    if(operation=="receive"&&(p.PartNumber.StartsWith("DEMO-",StringComparison.OrdinalIgnoreCase)||string.IsNullOrWhiteSpace(p.CatalogueReference)))return Bad("Enter the genuine part number and catalogue/supplier reference in Part Master before receiving stock. Legacy balances remain visible for reconciliation.");
    var s=await db.PartStocks.SingleOrDefaultAsync(x=>x.PartMasterId==p.Id&&x.InventoryLocationId==l.Id);if(s==null){s=new PartStock{PartMasterId=p.Id,InventoryLocationId=l.Id};db.PartStocks.Add(s);}
    var before=s.OnHandQty;decimal qty=input.Quantity;
    if(operation=="count"){
     var requests=await db.PartRequests.Where(x=>x.PartMasterId==p.Id&&x.InventoryLocationId==l.Id&&x.Status!="Cancelled").ToListAsync();
     if(requests.Any(x=>InventoryRules.ReservedRemaining(x)<0||InventoryRules.AtTechnician(x)<0))return Bad("Request history is inconsistent; review the affected requests before counting.");
     var reserved=requests.Sum(InventoryRules.ReservedRemaining);if(input.Quantity<reserved)return Bad("Count is below reserved quantity. Close or release outstanding reservations first.");
     var movements=await db.PartTransactions.Where(x=>x.PartMasterId==p.Id&&x.InventoryLocationId==l.Id).ToListAsync();qty=input.Quantity-movements.Sum(InventoryRules.StockDelta);s.OnHandQty=input.Quantity;s.ReservedQty=reserved;
    }else s.OnHandQty+=qty;
    s.UpdatedAt=DateTime.UtcNow;
    var waiting=await db.PartRequests.Where(x=>x.PartMasterId==p.Id&&x.InventoryLocationId==l.Id&&x.Status=="Awaiting Stock").ToListAsync();foreach(var request in waiting){if(s.OnHandQty-s.ReservedQty>=request.QuantityRequired-request.QuantityReserved){request.Status="Requested";request.UpdatedAt=DateTime.UtcNow;}}
    db.PartTransactions.Add(new PartTransaction{Id=input.OperationId??Guid.NewGuid(),PartMasterId=p.Id,InventoryLocationId=l.Id,PartNumber=p.PartNumber,PartDescription=p.Description,TransactionType=operation=="receive"?"Receipt":"Stock Count",Quantity=qty,UnitCost=p.StandardCost,ExtendedCost=Math.Round(qty*p.StandardCost,2),PerformedBy=input.User,Reference=input.Reference});
    ControlEndpoints.Log(db,operation.ToUpperInvariant(),"PartStock",s.Id,$"{p.PartNumber}; booked {before} -> {s.OnHandQty}; ledger adjustment {qty}; {input.Reference}",input.User);await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(s);
   });
  }
  app.MapGet("/api/parts/reconciliation",async(AppDbContext db)=>{
   var stocks=await db.PartStocks.AsNoTracking().ToListAsync();var parts=await db.PartMasters.AsNoTracking().ToDictionaryAsync(x=>x.Id);var locations=await db.InventoryLocations.AsNoTracking().ToDictionaryAsync(x=>x.Id);var tx=await db.PartTransactions.AsNoTracking().ToListAsync();var requests=await db.PartRequests.AsNoTracking().ToListAsync();
   return Results.Ok(stocks.Select(s=>{var moves=tx.Where(t=>t.PartMasterId==s.PartMasterId&&t.InventoryLocationId==s.InventoryLocationId);var rows=requests.Where(r=>r.PartMasterId==s.PartMasterId&&r.InventoryLocationId==s.InventoryLocationId);var ledger=moves.Sum(InventoryRules.StockDelta);var reserved=rows.Where(r=>r.Status!="Cancelled").Sum(InventoryRules.ReservedRemaining);return new{s.Id,s.PartMasterId,s.InventoryLocationId,partNumber=parts[s.PartMasterId].PartNumber,location=locations[s.InventoryLocationId].LocationCode,s.OnHandQty,s.ReservedQty,ledgerQty=ledger,quantityDifference=s.OnHandQty-ledger,reservationDifference=s.ReservedQty-reserved,issued=rows.Sum(r=>r.QuantityIssued),returned=rows.Sum(r=>r.QuantityReturned),consumed=rows.Sum(r=>r.QuantityConsumed),atTechnician=rows.Sum(InventoryRules.AtTechnician)};}));
  });
  app.MapPost("/api/parts/master/{id:guid}/photo",async(Guid id,HttpRequest request,AppDbContext db)=>{
   var p=await db.PartMasters.FindAsync(id);if(p==null)return Results.NotFound();if(!request.HasFormContentType)return Bad("Choose an image.");var form=await request.ReadFormAsync();var file=form.Files.GetFile("file");var source=form["source"].ToString();var user=form["user"].ToString();if(file==null||file.Length==0||file.Length>5*1024*1024||string.IsNullOrWhiteSpace(source)||string.IsNullOrWhiteSpace(user))return Bad("Choose a PNG/JPEG up to 5 MB, enter its source/reference and operator.");
   using var ms=new MemoryStream();await file.CopyToAsync(ms);var bytes=ms.ToArray();var png=bytes.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10});var jpeg=bytes.Length>3&&bytes[0]==255&&bytes[1]==216&&bytes[2]==255;if(!png&&!jpeg)return Bad("Only actual PNG/JPEG images are accepted.");
   p.Photo=bytes;p.PhotoType=png?"image/png":"image/jpeg";p.PhotoSource=source;ControlEndpoints.Log(db,"PHOTO","PartMaster",id,p.PartNumber+": "+source,user);await db.SaveChangesAsync();return Results.Ok(new{p.Id});
  });
  app.MapGet("/api/parts/master/{id:guid}/photo",async(Guid id,AppDbContext db,HttpResponse response)=>{var p=await db.PartMasters.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id);if(p==null||p.Photo.Length==0)return Results.NotFound();response.Headers["X-Content-Type-Options"]="nosniff";response.Headers["Cache-Control"]="no-store";return Results.File(p.Photo,p.PhotoType);});
 }
}
