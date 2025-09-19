using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuranVideoGeneration.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddHttpClient();

builder.Services.AddScoped<IPexelsService, PexelsService>();
builder.Services.AddScoped<IRecitationService, RecitationService>();
builder.Services.AddScoped<ICaptionService, CaptionService>();
builder.Services.AddScoped<IMediaService, MediaService>();

// Needed for Turnstile server-side verification (HttpClient)
builder.Services.AddHttpClient();
// Optional but recommended: match the header you’re sending in fetch()
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");
// Optional: only if any service/controller reads HttpContext via DI
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
