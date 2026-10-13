using ComunioAdminTools.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ComunioAdminTools.Core;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddComunioCore();
builder.Services.AddSingleton<ComunioAdminTools.UI.ResultsDialogController>();
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
