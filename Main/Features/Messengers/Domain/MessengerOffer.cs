namespace TAOM.Features.Messengers.Domain;

/// <summary>
/// What sending a messenger to one target would mean right now (#898): whether it may be sent, what it costs and
/// about how many days it takes. Built once per look by <see cref="TAOM.Features.Messengers.IMessengerService.GetOffer"/>, so the
/// dialog, the send and the encyclopedia button read the same quote.
/// </summary>
public sealed class MessengerOffer
{
    public MessengerOffer(MessengerValidationResult validation, int cost, int days)
    {
        Validation = validation;
        Cost = cost;
        Days = days;
    }

    public MessengerValidationResult Validation { get; }

    public int Cost { get; }

    public int Days { get; }

    public bool CanSend => Validation == MessengerValidationResult.Ok;
}
