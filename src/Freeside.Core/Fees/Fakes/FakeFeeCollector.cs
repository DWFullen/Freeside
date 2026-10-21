using Freeside.Core.Fakes;

namespace Freeside.Core.Fees.Fakes;

/// <summary>
/// PLACEHOLDER(ach-provider): stands in for the ACH provider (D12) until there is an account.
/// Debits stay <see cref="DebitState.Pending"/> until a test settles or returns them. Regtest only.
/// </summary>
public sealed class FakeFeeCollector(TimeProvider time) : IFeeCollector, IFakeService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, DebitId> _byIdempotencyKey = new(StringComparer.Ordinal);
    private readonly Dictionary<DebitId, DebitStatus> _debits = [];

    public Task<DebitId> InitiateDebitAsync(DebitRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_lock)
        {
            if (!_byIdempotencyKey.TryGetValue(request.IdempotencyKey, out var id))
            {
                id = new DebitId("fake-debit-" + Guid.NewGuid().ToString("N"));
                _byIdempotencyKey[request.IdempotencyKey] = id;
                _debits[id] = new DebitStatus(id, DebitState.Pending, null, time.GetUtcNow());
            }

            return Task.FromResult(id);
        }
    }

    public Task<DebitStatus> GetDebitAsync(DebitId debit, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_debits.TryGetValue(debit, out var status)
                ? status
                : throw new InvalidOperationException($"No fake debit {debit}."));
        }
    }

    public void Settle(DebitId debit) => Update(debit, DebitState.Settled, null);

    /// <summary>A bank return, with its Nacha return code (for example <c>R01</c>).</summary>
    public void Return(DebitId debit, string returnCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(returnCode);
        Update(debit, DebitState.Returned, returnCode);
    }

    private void Update(DebitId debit, DebitState state, string? returnCode)
    {
        lock (_lock)
        {
            if (!_debits.ContainsKey(debit))
            {
                throw new InvalidOperationException($"No fake debit {debit}.");
            }

            _debits[debit] = new DebitStatus(debit, state, returnCode, time.GetUtcNow());
        }
    }
}
