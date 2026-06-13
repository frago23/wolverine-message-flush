namespace ReproApp;

// Command that kicks off the bug. Published to the durable "trigger" queue.
public record TriggerCommand(Guid CorrelationId, int MessageCount);

// The burst-published message. Routed to the durable, Sequential "downstream"
// queue whose slow handler creates the back-pressure that drops the wake-up.
public record DownstreamMessage(Guid CorrelationId, int Index);

// A trivial Marten document so each SaveChangesAsync in the trigger handler
// has real work to commit alongside the outbox envelopes.
public class TriggerProgress
{
    public Guid Id { get; set; }
    public Guid CorrelationId { get; set; }
    public int Index { get; set; }
}
