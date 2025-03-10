using System.Security.Cryptography;
using Artifacts.Components;
using Artifacts.Components.Services;
using Radzen;
using DotNetEnv;


DotNetEnv.Env.Load();
var token = Environment.GetEnvironmentVariable("TOKEN");
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();
builder.Services.AddRadzenComponents();
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddJsonFile(".env", optional: true, reloadOnChange: true);
// Register IHttpContextAccessor
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CharacterService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

// Configure Content Security Policy with nonce
app.Use(async (context, next) =>
{
    var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    context.Items["CSPNonce"] = nonce;
    // Allow inline styles for Radzen components
    context.Response.Headers["Content-Security-Policy"] = $"default-src 'self'; style-src 'self' 'unsafe-inline' 'nonce-{nonce}'; img-src 'self' data:; font-src 'self' data:;";
    await next();
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();