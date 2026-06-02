using AssistantApi.Infrastructure;
using IngestionService;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((cfg) => cfg.ReadFrom.Configuration(builder.Configuration));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
