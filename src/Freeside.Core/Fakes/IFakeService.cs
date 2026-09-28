namespace Freeside.Core.Fakes;

/// <summary>
/// Marks a stand-in for an external service we have no account or key for yet
/// (docs/plans/placeholders.md). Register fakes with
/// <see cref="FakeServiceCollectionExtensions.AddFake{TService, TFake}"/>; the host refuses to start
/// with a fake registered unless <c>Bitcoin:Network</c> is regtest.
/// </summary>
public interface IFakeService;
