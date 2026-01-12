using Template_Builder.Models.Entities;
using Template_Builder.Services.Implementations;
using Template_Builder.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Register services
builder.Services.AddScoped<ISqlConnectionService, SqlConnectionService>();
builder.Services.AddScoped<ISqlSchemaService, SqlSchemaService>();
builder.Services.AddScoped<ITemplateTypeService, TemplateTypeService>();
builder.Services.AddScoped<ISqlTemplateService, SqlTemplateService>();
builder.Services.AddScoped<ITraceService, TraceService>();

// Add configuration
builder.Services.Configure<List<ServerInfo>>(builder.Configuration.GetSection("Servers"));

// Add logging
builder.Services.AddLogging();

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
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

app.MapHealthChecks("/health");

app.Run();