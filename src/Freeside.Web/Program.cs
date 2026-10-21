using Freeside.Core.Bitcoin;
using Freeside.Core.Payments;
using Freeside.Infrastructure.Database;
using Freeside.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBitcoinNetwork();
builder.Services.AddFreesideDatabase();
builder.Services.AddFreesidePayments(builder.Configuration);
builder.Services.AddFreesideFees(builder.Configuration);

// Writers only (the inbox for webhooks, from PR 6). The worker runs the queue processors.
builder.Services.AddFreesideMessaging();
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Liveness only. Readiness checks (database, BTCPay) arrive with those dependencies.
app.MapHealthChecks("/healthz");
app.MapRazorPages();

app.Run();
