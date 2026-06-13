using JasperFx.Resources;
using Marten;
using ReproApp;
using Wolverine;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

// Bind to a fixed HTTP port so the curl commands in the README work as written.
builder.WebHost.UseUrls("http://localhost:5000");

const string ConnectionString =
    "Host=localhost;Port=5433;Database=repro;Username=repro;Password=repro";

builder.Host.UseWolverine(opts =>
{
    opts.Services.AddMarten(m =>
    {
        m.Connection(ConnectionString);
        m.DatabaseSchemaName = "repro";
        // Marten defaults AutoCreateSchemaObjects to CreateOrUpdate, so the
        // TriggerProgress table (and the Wolverine envelope tables) are built
        // on demand / at startup.
    })
    .IntegrateWithWolverine();

    // THESE THREE POLICIES TOGETHER ARE LOAD-BEARING FOR THE BUG.
    opts.Policies.AutoApplyTransactions();
    opts.Policies.UseDurableLocalQueues();
    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

    // Trigger queue -- receives the command that kicks off the bug.
    opts.LocalQueue("trigger")
        .UseDurableInbox()
        .Sequential();

    // Downstream queue -- destination of the burst-published messages.
    // MUST be Sequential and Durable so its in-memory channel is bounded
    // and slow to drain.
    opts.LocalQueue("downstream")
        .UseDurableInbox()
        .Sequential();

    opts.PublishMessage<TriggerCommand>().ToLocalQueue("trigger");
    opts.PublishMessage<DownstreamMessage>().ToLocalQueue("downstream");
});

// Ensure Wolverine + Marten create the wolverine_* tables on first run.
builder.Services.AddResourceSetupOnStartup();

var app = builder.Build();

// Trigger endpoint -- kicks off the bug.
app.MapPost("/trigger/{count:int}", async (int count, IMessageBus bus) =>
{
    var correlationId = Guid.CreateVersion7();
    await bus.PublishAsync(new TriggerCommand(correlationId, count));
    return Results.Ok(new { correlationId, count });
});

app.MapGet("/", () => Results.Ok(new { status = "up", trigger = "POST /trigger/{count}" }));

await app.RunAsync();
