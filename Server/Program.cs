// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Bsp.Server;
using Microsoft.Extensions.Logging;

// var builder = Host.CreateDefaultBuilder(args);
var builder = Host.CreateApplicationBuilder(args);
var config = new ConfigurationBuilder().SetBasePath(builder.Environment.ContentRootPath)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables().Build();

var s = builder.Services;
builder.Logging.AddConsole();
// builder.ConfigureServices(s =>
{
    s.Configure<CommandHostConfiguration>(config.GetSection("CommandHost"));
    s.AddHostedService<CommandHostedService>();
    s.AddScoped<ICommandController, CommandController>();
}//)
builder.Build().Run();