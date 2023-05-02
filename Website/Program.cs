using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;

using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;

using System.Net;

using Website.Data;
using Website.Data.Ocms;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls("http://*:80");

builder.Configuration.SetBasePath(AppDomain.CurrentDomain.BaseDirectory).AddJsonFile("appsettings.json", false, true).AddEnvironmentVariables();

//Add services to the container.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.KnownProxies.Add(IPAddress.Parse("152.228.161.65"));
});

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<ProtectedSessionStorage>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IvaoLoginService>();
builder.Services.AddDbContextFactory<WebsiteContext>(options => options.UseMySQL($"server=division.ivao.aero;database=xaivao_web;user=xaivao_web;password={builder.Configuration["xaivao_web_password"]}", msoa => msoa.CommandTimeout(5)));
builder.Services.AddSingleton<WhazzupService>();
builder.Services.AddSingleton<IvaoApiService>();
builder.Services.AddSingleton<DiscordService>();
builder.Services.AddSingleton<OccStrips>();
builder.Services.AddSingleton<DatalinkService>();
builder.Services.AddBlazorise(o => o.Immediate = true).AddBootstrap5Providers().AddFontAwesomeIcons();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions {
	ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Trigger the updating thread
app.Services.GetService<WhazzupService>();
app.Services.GetService<DiscordService>();

app.Run();