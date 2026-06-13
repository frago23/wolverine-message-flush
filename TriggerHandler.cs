using Marten;
using Wolverine;
using Wolverine.Attributes;

namespace ReproApp;

// The buggy handler. It interleaves SaveChangesAsync with PublishAsync inside a
// loop. Each SaveChangesAsync transactionally flushes the PREVIOUS iteration's
// pending outgoing envelope into wolverine_incoming_envelopes (the destination
// is a durable local queue), then fires a best-effort, post-commit "wake-up"
// signal at the downstream listener's bounded in-memory channel. While the
// Sequential downstream consumer is busy with slow work, that channel is full
// and the wake-up is dropped silently -> the envelope is durable in the DB but
// the in-process node never picks it up until the durability agent / restart
// recovery sweep finds it.
public static class TriggerHandler
{
    [WolverineHandler]
    [MessageTimeout(300)]
    public static async Task Handle(
        TriggerCommand command,
        IDocumentSession session,
        IMessageBus bus,
        CancellationToken ct)
    {
        for (var i = 0; i < command.MessageCount; i++)
        {
            // Append SOMETHING to the session so SaveChangesAsync has work.
            session.Store(new TriggerProgress
            {
                Id = Guid.CreateVersion7(),
                CorrelationId = command.CorrelationId,
                Index = i
            });

            // 1. SaveChangesAsync first -- flushes any prior pending outgoing
            //    envelopes from the message context (and fires their wake-ups).
            await session.SaveChangesAsync(ct);

            // 2. Then publish -- this queues the envelope in the message
            //    context. It is persisted on the NEXT SaveChangesAsync (the next
            //    loop iteration), and Wolverine fires a best-effort wake-up at
            //    the downstream listener.
            await bus.PublishAsync(new DownstreamMessage(command.CorrelationId, i));
        }

        // Final save to flush the last published message.
        session.Store(new TriggerProgress
        {
            Id = Guid.CreateVersion7(),
            CorrelationId = command.CorrelationId,
            Index = -1
        });
        await session.SaveChangesAsync(ct);
    }
}
