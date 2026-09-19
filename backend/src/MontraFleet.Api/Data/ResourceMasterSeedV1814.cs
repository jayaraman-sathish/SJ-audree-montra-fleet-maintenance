using Microsoft.EntityFrameworkCore;
using MontraFleet.Api.Models;
namespace MontraFleet.Api.Data;

// Sample names and catalogue records for the demo fleet, not OEM fitment data.
// Stable codes make restarts additive. Never overwrite edited or inactive records.
public static class ResourceMasterSeedV1814
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var names = new[]{"Ram", "Anand", "Gokul", "Priya S"};
        for(var i=0;i<names.Length;i++)
        {
            var code=$"SUP-DEMO-{i+1:D3}";
            if(!await db.MasterOptions.AnyAsync(x=>x.Category=="SUPERVISOR"&&x.Code==code))
                db.MasterOptions.Add(new MasterOption{Category="SUPERVISOR",Code=code,Name=names[i],Description="Sample supervisor — replace with your staff details",SortOrder=i+1});
        }
        var technicians = new[]{("TECH001","Suresh K","PM,BRAKE"),("TECH002","Meena P","EV,DIAG"),("TECH-DEMO-003","Arun Kumar","PM,DIAG"),("TECH-DEMO-004","Ravi Teja","PM,BRAKE"),("TECH-DEMO-005","Karthik R","EV,DIAG"),("TECH-DEMO-006","Lakshmi P","PM,DIAG")};
        foreach(var (code,name,skills) in technicians)
            if(!await db.Technicians.AnyAsync(x=>x.EmployeeCode==code))
                db.Technicians.Add(new Technician{EmployeeCode=code,Name=name,SkillCodes=skills,ServiceCentre="Demo Service Centre",HvAuthorized=false,HourlyRate=0});
        // Genuine parts are entered from the service centre catalogue; no demo parts are seeded.
        await db.SaveChangesAsync();
    }
}
