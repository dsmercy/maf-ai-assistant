using AssistantApi.Infrastructure;
using AssistantApi.Infrastructure.Ingestion;
using IngestionService;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((cfg) => cfg.ReadFrom.Configuration(builder.Configuration));
builder.Services.AddInfrastructure(builder.Configuration);

// Resolve relative ingestion paths against the application's content root so that
// "data/uploads" becomes "<project-dir>/data/uploads" when running locally,
// while absolute Docker paths like "/data/uploads" are left unchanged.
// PostConfigure runs after all Configure calls (including AddInfrastructure) so it wins.
builder.Services.PostConfigure<IngestionOptions>(opts =>
{
    var root = builder.Environment.ContentRootPath;
    if (!Path.IsPathRooted(opts.UploadPath))
        opts.UploadPath = Path.GetFullPath(Path.Combine(root, opts.UploadPath));
    if (!Path.IsPathRooted(opts.RepositoryCachePath))
        opts.RepositoryCachePath = Path.GetFullPath(Path.Combine(root, opts.RepositoryCachePath));
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Ensure the database schema exists — required when running locally against assistant_db_dev
// which is a fresh database. In Docker the API container runs first and creates the schema.
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AssistantApi.Infrastructure.Persistence.AssistantDbContext>();
    db.Database.EnsureCreated();
}

host.Run();
