using Freeside.Worker;

var builder = Host.CreateApplicationBuilder(args);
WorkerServices.Configure(builder.Services, builder.Configuration);
builder.Build().Run();
