namespace Freeside.Regtest.Tests;

/// <summary>
/// Endpoints and throwaway credentials of the stack in tools/regtest/compose.yml.
/// Keep them in step with that file. They are valid on regtest only.
/// </summary>
internal static class RegtestStack
{
    public static readonly Uri BtcpayUrl = new("http://127.0.0.1:49392/");

    public static readonly Uri BitcoindRpcUrl = new("http://127.0.0.1:43782/");

    public const string BitcoindRpcCredentials = "freeside:regtest-throwaway";

    /// <summary>BTCPay's first user, created by the first test run on a fresh stack.</summary>
    public const string AdminEmail = "admin@freeside.test";

    public const string AdminPassword = "regtest-throwaway";

    public const string OnChainPaymentMethod = "BTC-CHAIN";
}
