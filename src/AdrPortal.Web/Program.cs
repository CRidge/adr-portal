using AdrPortal.Web.Components;
using AdrPortal.Web.Services;
using AdrPortal.Web.State;
using AdrPortal.Infrastructure.Data;
using AdrPortal.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<RepositoryCatalogState>();
builder.Services.AddScoped<IMadrRepositoryFactory, MadrRepositoryFactory>();
builder.Services.AddScoped<AdrDocumentService>();
builder.Services.AddScoped<GlobalLibraryService>();
builder.Services.AddSingleton<AdrListViewService>();
builder.Services.AddSingleton<IAdrMarkdownRenderer, AdrMarkdownRenderer>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AdrPortalDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

app.Run();
