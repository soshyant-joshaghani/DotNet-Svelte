using DotnetSvelte;
using DotnetSvelte.Core.Config;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddDotEnv();
builder.WebHost.UseUrls(
    $"http://{builder.Configuration["APP_HOST"] ?? "0.0.0.0"}:{builder.Configuration["APP_PORT"] ?? "8000"}");
builder.Services.AddFoxg();

var app = builder.Build();
app.UseFoxg();
app.Run();

public partial class Program;
