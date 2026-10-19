# Dogs responding to broken glass

The museum starts without visible dogs. Breaking any of its seven exhibit cases,
with either the hammer or a gun, calls in three dogs. They arrive 0.65 seconds
apart from the central hall, west gallery, and east gallery, then navigate toward
the broken case. Breaking another case redirects the same pack to that location.
There is one pack per scene load, rather than three additional dogs per break.
Restarting restores intact glass and removes the pack.

## Runtime components

`GlassHoundResponse.cs` subscribes to the existing `BreakableGlass.OnBroken`
UnityEvent for each configured case. It instantiates the response prefab at the
entry transforms only after a real break and sends the case position to each dog.
It removes listeners when disabled and caps spawning at one dog per entry.
No changes were needed in BreakableGlass or the existing AI/Hound files.

This is an explicit gameplay response to a glass-break event. It does not depend
on microphone input, AudioSource volume, or the old HoundHearing range and memory.

`HoundInvestigator.cs` gives each dog a NavMesh destination. A display pedestal
may not be walkable, so it samples nearby floor positions and chooses a reachable
approach with a complete path. It retries routing while travelling, stops near
the clue, and accepts a new clue when another case breaks. Dogs investigate the
glass location; player tracking, bites, damage, and despawning are future work.

`HoundLocomotion.cs` uses navigation speed to animate diagonal pairs of legs,
layered over the existing breathing and tail idle. It returns the legs to their
rest rotations when stopped. The Animator uses no root motion: the NavMeshAgent
controls translation and facing.

## Assets and scene setup

- `MuseumHound.prefab` remains the reusable presentation-only dog.
- `InvestigatingHound.prefab` is a variant adding a NavMeshAgent, investigator,
  and locomotion component. Speed is 3.2 metres/second; stopping distance is 0.65m.
- `Museum_Main` contains an empty `GlassHoundResponse` director, seven glass
  references, and three entry markers. Its children contain no dog renderers
  before a break.

| Entry | World position |
| --- | --- |
| Central hall | (0, 0.05, 7) |
| West gallery | (-9, 0.05, 25) |
| East gallery | (9, 0.05, 25) |

Select `GlassHoundResponse` in the Hierarchy to adjust entry transforms and the
arrival interval. Its prefab reference controls which dog is spawned. The baked
NavMesh must remain available at each entry. Moving a marker outside the NavMesh
can prevent that entry from spawning a dog.

`Tools > Lockdown > Build Glass Hound Response` rebuilds the variant and restores
the three default entry positions and all exhibit references. It removes the
earlier standalone MuseumHound presentation instance. Save after running the menu.
Rebuilding the presentation model alone does not configure the glass response.

## Verification

All 20 Play Mode tests passed on 19 October 2026. The response tests check no dogs
before a break, three distinct arrivals, actual movement and arrival near a case,
all seven glass events, reachable approaches, pack reuse, restart cleanup, and
listener cleanup. Presentation, door, hammer, and glass regression tests also pass.
Local screenshots and exported results are in `Temp/HoundResponseVerification`.
