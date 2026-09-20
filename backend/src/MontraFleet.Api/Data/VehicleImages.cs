using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Models;
namespace MontraFleet.Api.Data;
public static class VehicleImages
{
 public static string Resolve(string? vehicleImage,string? variantImage,string? modelImage,string? typeImage=null)
 {
  foreach(var image in new[]{vehicleImage,variantImage,modelImage,typeImage})
   if(!string.IsNullOrWhiteSpace(image))return image.Trim();
  return "";
 }
 // Product reference images published on https://www.montraelectric.com/ (20 Sep 2026).
 // e-45 is the manufacturer's silhouette, not a photograph. Unmatched products have no substitute.
 public static readonly IReadOnlyDictionary<string,string> Products=new Dictionary<string,string>{
  ["SUPER_AUTO"]="https://cdn.prod.website-files.com/6938169ca88035d1476d6905/69b153b7149060ba5381f207_Screenshot_2026-03-11_162620-removebg-preview%20(1).png",
  ["SUPER_CARGO"]="https://cdn.prod.website-files.com/6938169ca88035d1476d6905/69b153b7149060ba5381f210_a070e195e0136d55c37bd83943677e463243c567.png",
  ["EVIATOR"]="https://cdn.prod.website-files.com/6938169ca88035d1476d6905/6a685d5abea1383ff7495a76_Gemini_Generated_Image_yt8wv0yt8wv0yt8w-removebg-preview.png",
  ["RHINO_5538_EV"]="https://cdn.prod.website-files.com/6938169ca88035d1476d6905/69fc3829eb1ca0ce6154bdfc_4X2_%20Driver.webp",
  ["RH5538-4X2"]="https://cdn.prod.website-files.com/6938169ca88035d1476d6905/69fc3829eb1ca0ce6154bdfc_4X2_%20Driver.webp",
  ["RH5538-6X4"]="https://cdn.prod.website-files.com/6938169ca88035d1476d6905/69b153b7149060ba5381f22e_Screenshot%202026-03-11%20165517.png",
  ["TRACTOR_E27"]="https://cdn.prod.website-files.com/6938169ca88035d1476d6905/69b153b7149060ba5381f234_Screenshot%202026-03-11%20170329.png",
  ["TRACTOR_E45"]="https://cdn.prod.website-files.com/6938169ca88035d1476d6905/6a9e74e6d623c9247d117916_tractor%20silhouette.png",
 };
 public static async Task SeedReferences(AppDbContext db){
  foreach(var m in await db.VehicleModelMasters.Where(x=>x.ManufacturerCode=="MONTRA").ToListAsync()){
   if(string.IsNullOrWhiteSpace(m.ImageUrl)&&Products.TryGetValue(m.ModelCode,out var image))m.ImageUrl=image;
   if(m.ModelCode=="RHINO_5538_EV")foreach(var v in await db.VehicleVariantMasters.Where(x=>x.VehicleModelMasterId==m.Id).ToListAsync())
    if(string.IsNullOrWhiteSpace(v.ImageUrl)&&Products.TryGetValue(v.VariantCode,out var variantImage))v.ImageUrl=variantImage;
  }
  await db.SaveChangesAsync();
 }
 // Apply to detached API records only; keep the stored vehicle photo as an explicit override.
 public static async Task Populate(AppDbContext db,IEnumerable<Vehicle> vehicles){
  var models=await db.VehicleModelMasters.AsNoTracking().ToDictionaryAsync(x=>x.Id);
  var variants=await db.VehicleVariantMasters.AsNoTracking().ToDictionaryAsync(x=>x.Id);
  var types=await db.MasterOptions.AsNoTracking().Where(x=>x.Category=="VEHICLE_TYPE").ToDictionaryAsync(x=>x.Code);
  foreach(var v in vehicles){
   var model=v.ModelMasterId.HasValue?models.GetValueOrDefault(v.ModelMasterId.Value):null;
   var variant=v.VariantMasterId.HasValue?variants.GetValueOrDefault(v.VariantMasterId.Value):null;
   v.ImageUrl=Resolve(v.ImageUrl,variant?.ImageUrl,model?.ImageUrl,model is null?null:types.GetValueOrDefault(model.VehicleTypeCode)?.ImageUrl);
  }
 }
}
