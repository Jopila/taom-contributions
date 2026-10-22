namespace TAOM.Features.CoopInterop;

/// <summary>
/// Whether this process is a Bannerlord DEDICATED SERVER — a headless host with no local player.
///
/// The distinction matters because <c>Hero.MainHero</c> exists there anyway: it is the world-gen
/// hero the campaign was created around, idle and unplayed. Anything that credits, charges or
/// rewards "the player" by reading <c>Hero.MainHero</c> therefore acts on a hero nobody controls.
/// Field report 2026-08-03 §6 caught this as dozens of <c>[SpecRes] PRISONERS: +N</c> lines on a
/// server while the remote players who actually fought those battles earned nothing.
///
/// It is deliberately NOT derived from co-op role, because it is a fact about the process. It matters
/// only with no session live: during one, the player registry decides who is a player, and the co-op
/// "host" is a loopback client of a separate server process, so no human plays on the authority process
/// either way (#967 design comment, https://github.com/haterade22/TAOM/issues/967#issuecomment-6328464305).
/// </summary>
public interface IDedicatedServerProvider
{
    bool IsDedicatedServer { get; }
}
