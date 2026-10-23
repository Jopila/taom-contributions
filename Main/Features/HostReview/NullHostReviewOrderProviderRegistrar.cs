namespace TAOM.Features.HostReview;

/// <summary>Core's default (ADR-013): registers nothing. TAOM.Client replaces it on a rendering client.</summary>
public sealed class NullHostReviewOrderProviderRegistrar : IHostReviewOrderProviderRegistrar
{
    public void Register() { }
}
