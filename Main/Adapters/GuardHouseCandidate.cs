namespace TAOM.Adapters;

/// <summary>
/// A town's or castle's Guard House as the AI build pick sees it (#889), with no TaleWorlds type in it (ADR-007).
/// <c>default</c> reads as "not eligible", so a settlement without the building answers no.
/// </summary>
/// <param name="Eligible">
/// The Guard House passes vanilla's own filter for the next build: not a daily project, level below 3, not already queued.
/// </param>
/// <param name="Level">The Guard House level, 0 when it is not built yet.</param>
/// <param name="Roll">A random number from 0 up to but not including 1, drawn by the adapter so the service stays pure.</param>
public readonly record struct GuardHouseCandidate(bool Eligible, int Level, float Roll);
