using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
//using Microsoft.Graph;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using TaxForm.Models;

var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
//.AddCookie(x => x.LoginPath = "/account/login") ;
// Add services to the container.
//builder.Services.AddScoped<GraphServiceClient, GraphServiceClient>();

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));//, OpenIdConnectDefaults.AuthenticationScheme, "ADCookies");

builder.Services.AddAuthorization(options =>
{
    // By default, all incoming requests will be authorized according to the default policy.
    options.FallbackPolicy = options.DefaultPolicy;
});
//builder.Services.AddRazorPages()
//    .AddMicrosoftIdentityUI();

builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();

builder.Services.AddDbContext<TaxFormReaderContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TaxFormReaderConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

//app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=TrTaxes}/{action=Index}/{id?}");

app.Run();
