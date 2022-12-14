using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;

using Website.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.SetBasePath(AppDomain.CurrentDomain.BaseDirectory).AddJsonFile("appsettings.json", false, true).AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<ProtectedSessionStorage>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IvaoLoginService>();
builder.Services.AddDbContextFactory<WebsiteContext>(options => options.UseMySQL($"server=xa.ivao.aero;database=xaivao_web;user=xaivao_web;password={builder.Configuration["xaivao_web_password"]}"));
builder.Services.AddSingleton<WhazzupService>();
builder.Services.AddSingleton<IvaoApiService>();
builder.Services.AddSingleton<DiscordService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Trigger the updating thread
app.Services.GetService<WhazzupService>();
app.Services.GetService<DiscordService>();

app.Run();