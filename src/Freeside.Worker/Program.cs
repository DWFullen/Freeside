using Freeside.Worker;

var builder = Host.CreateApplicationBuilder(args);
WorkerServices.Configure(builder.Services);
builder.Build().Run();
