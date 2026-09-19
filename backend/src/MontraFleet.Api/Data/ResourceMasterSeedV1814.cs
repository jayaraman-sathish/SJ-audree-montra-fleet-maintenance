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
        var models=await db.VehicleModelMasters.Where(x=>x.IsActive).ToListAsync();
        var catalogue=new[]{("BRAKE","Brake friction kit","brake"),("TYRE","Tyre service replacement","tyre"),("FUSE","Electrical fuse kit","fuse"),("BEARING","Bearing service kit","bearing"),("WIPER","Wiper blade","wiper"),("COOLANT","Cooling-system service fluid","coolant")};
        foreach(var model in models)
        {
            var tasks=await (from program in db.MaintenancePrograms where program.VehicleModelMasterId==model.Id&&program.IsActive
                join plan in db.MaintenancePlans on program.Id equals plan.MaintenanceProgramId where plan.IsActive
                join matrix in db.MaintenancePlanMatrixItems on plan.Id equals matrix.MaintenancePlanId
                join task in db.MaintenanceTaskDefinitions on matrix.MaintenanceTaskDefinitionId equals task.Id where task.IsActive
                select new{task.TaskCode,task.TaskName}).Distinct().ToListAsync();
            foreach(var (code,name,keyword) in catalogue)
            {
                var matching=tasks.Where(x=>x.TaskName.Contains(keyword,StringComparison.OrdinalIgnoreCase)).ToList();
                if(matching.Count==0)continue;
                var partNumber=$"DEMO-{model.ModelCode}-{code}";
                if(!await db.PartMasters.AnyAsync(x=>x.PartNumber==partNumber))
                    db.PartMasters.Add(new PartMaster{PartNumber=partNumber,Description=$"{model.Name} · {name} (demo; verify OEM specification)",Category="Demo / "+code,UnitOfMeasure=code=="COOLANT"?"L":"EA",StandardCost=0});
                if(!await db.MasterOptions.AnyAsync(x=>x.Category=="PART_MODEL"&&x.Code==partNumber))
                    db.MasterOptions.Add(new MasterOption{Category="PART_MODEL",Code=partNumber,Name=name,Value=model.ModelCode,Description=string.Join(", ",matching.Select(x=>x.TaskCode).Distinct())});
            }
        }
        await db.SaveChangesAsync();
    }
}
