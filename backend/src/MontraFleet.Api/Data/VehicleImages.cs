namespace MontraFleet.Api.Data;
public static class VehicleImages
{
 public static string Resolve(string? vehicleImage,string? variantImage,string? modelImage)
 {
  foreach(var image in new[]{vehicleImage,variantImage,modelImage})
   if(!string.IsNullOrWhiteSpace(image))return image.Trim();
  return "";
 }
}
