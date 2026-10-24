namespace TAOM.Features.KeepWardens;

/// <summary>
/// The service's answer for one settlement: whether a NEW castle patrol may be queued, and the template strength and spawn
/// delay. <c>default</c> (strength 0) means "not a castle patrol request", so the base model (DefaultSettlementPatrolModel) answers.
/// </summary>
/// <param name="Allowed">
/// True when a new patrol may be queued: the switch is on, the owner is not a rebel and the Guard House is built. Only
/// <c>CanSettlementHavePatrolParties</c> and the spawn delay read it.
/// </param>
/// <param name="Strength">
/// 1 (weak), 2 (moderate) or 3 (strong) template, from the Guard House level; 0 when the base model answers. Filled on plans
/// that are not <paramref name="Allowed"/> too, so a live patrol the plan refuses still gets a template instead of a null one.
/// </param>
/// <param name="SpawnDays">The configured spawn delay for the strength; filled whenever <paramref name="Strength"/> is.</param>
public readonly record struct CastlePatrolPlan(bool Allowed, int Strength, float SpawnDays);
