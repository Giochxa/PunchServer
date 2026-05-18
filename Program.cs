using LiteDB;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PunchServerMVC.Data;
using PunchServerMVC.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<PunchRepository>();
builder.Services.AddSingleton<IRepository, PunchRepository>();
builder.Services.AddHostedService<AutoPunchOutWorker>();
builder.Services.AddHostedService<DatabaseBackupWorker>();
builder.Services.AddHostedService<ProfilePictureAssignmentWorker>();
builder.Host.UseWindowsService();
// builder.Services.AddSingleton<IOrganisationRepository, OrganisationRepository>();

builder.WebHost.UseUrls(
    "http://0.0.0.0:5067"
);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
