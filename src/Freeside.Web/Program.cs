using Freeside.Core.Bitcoin;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBitcoinNetwork();
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Liveness only. Readiness checks (database, BTCPay) arrive with those dependencies.
app.MapHealthChecks("/healthz");
app.MapRazorPages();

app.Run();
