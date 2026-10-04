namespace TTNOverlay.Services;

/// <summary>Outcome of a "delete chat message(s)" call, distinguishing the one case the user can fix by logging in again.</summary>
public enum ModerationDeleteResult
{
    Ok,

    /// <summary>
    /// 401: the token lacks moderator:manage:chat_messages (the user logged in before that scope was added) or is
    /// invalid. The user can fix this by logging out and in again.
    /// </summary>
    MissingPermission,

    /// <summary>
    /// Twitch refused this particular delete: the message is too old, already gone, or belongs to the broadcaster or
    /// another moderator. Nothing to fix on the user's side.
    /// </summary>
    Rejected,

    /// <summary>Network error, rate limit, 403 (the account is not a moderator of that channel), or anything else unexpected.</summary>
    Failed,
}

/// <summary>Outcome of allowing/denying a message held by AutoMod.</summary>
public enum AutoModDecisionResult
{
    Ok,

    /// <summary>401: the token lacks moderator:manage:automod (logged in before that scope was added) or is invalid.</summary>
    MissingPermission,

    /// <summary>Twitch no longer has that message in the queue: another moderator handled it, or it expired.</summary>
    AlreadyResolved,

    /// <summary>Network error, rate limit, 403 (not a moderator of that channel), or anything else unexpected.</summary>
    Failed,
}

internal enum ModerationSanction
{
    None,
    Warn,
    Timeout,
    Ban,
}

/// <summary>One moderator action on a chatter: optionally delete a message and/or apply a sanction.</summary>
internal sealed record ModerationActionRequest(
    string Channel,
    string UserId,
    string? MessageIdToDelete,
    ModerationSanction Sanction,
    int TimeoutSeconds = 0,
    string WarnReason = ""
);

/// <summary>What happened for each half of an action; null means that half wasn't requested.</summary>
internal readonly record struct ModerationActionOutcome(ModerationDeleteResult? Delete, bool? Sanction)
{
    public bool AllSucceeded => (Delete is null or ModerationDeleteResult.Ok) && Sanction is null or true;
}

/// <summary>Totals for a bulk delete.</summary>
internal readonly record struct BulkDeleteOutcome(int Deleted, int Rejected, int Failed, bool MissingPermission);

/// <summary>
/// Runs moderation actions against an <see cref="IModerationService"/>. Kept free of any UI so the sequencing and
/// failure handling can be unit-tested.
/// </summary>
internal static class ModerationActionRunner
{
    /// <summary>How many delete requests a bulk delete keeps in flight (Helix allows far more; this keeps the burst gentle).</summary>
    public const int BulkDeleteConcurrency = 4;

    /// <summary>
    /// Deletes and sanctions in parallel: the two calls are independent, so the moderator waits for the slower one
    /// instead of the sum. A failed delete (message already gone, too old, or from another mod) never blocks the
    /// sanction, which is usually the part the moderator actually cares about.
    /// </summary>
    public static async Task<ModerationActionOutcome> RunAsync(IModerationService service, ModerationActionRequest request)
    {
        Task<ModerationDeleteResult>? delete = request.MessageIdToDelete is null
            ? null
            : GuardDelete(service.DeleteMessageAsync(request.Channel, request.MessageIdToDelete));

        Task<bool>? sanction = request.Sanction switch
        {
            ModerationSanction.Warn => Guard(service.WarnAsync(request.Channel, request.UserId, request.WarnReason)),
            ModerationSanction.Timeout => Guard(service.TimeoutAsync(request.Channel, request.UserId, request.TimeoutSeconds)),
            ModerationSanction.Ban => Guard(service.BanAsync(request.Channel, request.UserId)),
            _ => null,
        };

        ModerationDeleteResult? deleteResult = delete is null ? null : await delete;
        bool? sanctionResult = sanction is null ? null : await sanction;
        return new ModerationActionOutcome(deleteResult, sanctionResult);
    }

    /// <summary>
    /// Deletes many messages with a small fixed concurrency. If Twitch says the token lacks the permission, the
    /// remaining requests are skipped instead of firing hundreds of calls that are certain to fail.
    /// <paramref name="onMessageDeleted"/> is invoked once per successfully deleted message, from a worker thread.
    /// </summary>
    public static async Task<BulkDeleteOutcome> DeleteManyAsync(
        IModerationService service,
        string channel,
        IReadOnlyList<string> messageIds,
        Action<string>? onMessageDeleted = null,
        CancellationToken cancellationToken = default
    )
    {
        int deleted = 0,
            rejected = 0,
            failed = 0;
        int nextIndex = -1;
        bool missingPermission = false;

        async Task WorkerAsync()
        {
            while (!cancellationToken.IsCancellationRequested && !Volatile.Read(ref missingPermission))
            {
                int index = Interlocked.Increment(ref nextIndex);
                if (index >= messageIds.Count)
                    return;

                var messageId = messageIds[index];
                switch (await GuardDelete(service.DeleteMessageAsync(channel, messageId)))
                {
                    case ModerationDeleteResult.Ok:
                        Interlocked.Increment(ref deleted);
                        NotifyDeleted(onMessageDeleted, messageId);
                        break;
                    case ModerationDeleteResult.Rejected:
                        Interlocked.Increment(ref rejected);
                        break;
                    case ModerationDeleteResult.MissingPermission:
                        Volatile.Write(ref missingPermission, true);
                        break;
                    default:
                        Interlocked.Increment(ref failed);
                        break;
                }
            }
        }

        int workers = Math.Min(BulkDeleteConcurrency, messageIds.Count);
        var tasks = new Task[workers];
        for (int i = 0; i < workers; i++)
            tasks[i] = Task.Run(WorkerAsync, CancellationToken.None);
        await Task.WhenAll(tasks);

        return new BulkDeleteOutcome(deleted, rejected, failed, Volatile.Read(ref missingPermission));
    }

    private static void NotifyDeleted(Action<string>? callback, string messageId)
    {
        try
        {
            callback?.Invoke(messageId);
        }
        catch (Exception ex)
        {
            // A faulty observer must not abort the rest of the bulk delete.
            DebugLog.WriteException("ModerationActionRunner.OnMessageDeleted", ex);
        }
    }

    private static async Task<ModerationDeleteResult> GuardDelete(Task<ModerationDeleteResult> task)
    {
        try
        {
            return await task;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("ModerationActionRunner.Delete", ex);
            return ModerationDeleteResult.Failed;
        }
    }

    private static async Task<bool> Guard(Task<bool> task)
    {
        try
        {
            return await task;
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("ModerationActionRunner.Sanction", ex);
            return false;
        }
    }
}
