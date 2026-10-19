# Glass-triggered hound response: viva notes

## Short explanation

“I connected exhibit glass breaking to a response director. There are no dogs
initially. When glass breaks, the director creates three dog prefabs at different
entry points and gives each the sound position. A separate navigation component
finds a reachable point beside the display and moves there. Later breaks redirect
the same dogs, so the number stays bounded.”

## How the parts connect

1. Hammer or bullet damage reduces the glass Health to zero.
2. BreakableGlass hides the glass, disables its collider, plays its effect and
   audio, and invokes its existing OnBroken UnityEvent.
3. GlassHoundResponse listens to that event. It records the case position and
   dispatches one dog from each of three entry transforms, with a short stagger.
4. Each InvestigatingHound prefab has a NavMeshAgent and HoundInvestigator. The
   investigator samples the floor around the glass and checks for a complete
   path. The glass itself may be over a non-walkable pedestal.
5. The agent moves and rotates the dog. HoundLocomotion animates diagonal leg
   pairs from its speed while the Animator continues the breathing and tail idle.
6. The dog stops near the broken case. Another break replaces its destination.

## Why I used separate components

The glass owns breaking and loot access. The director owns spawning and pack
size. The investigator owns destinations. The locomotion component owns the leg
pose. The model prefab owns the visible dog. These responsibilities can be
changed separately, and the existing AI/Hound scripts did not need editing.

The old HoundHearing script remembers clues within a hearing range. This feature
instead uses the exact glass event to call a museum response pack from multiple
locations. Audio playback and gameplay notification are separate systems.

## What prevents endless spawning?

The director records dispatched entry slots and reuses its existing dogs.
Breaking every exhibit still gives three dogs total. Repeated damage to an
already broken case cannot produce a second break event. Listener removal on
disable prevents duplicate subscriptions after re-enabling the director.

## What comes later?

Dogs investigate the sound position. They do not yet track the player, attack,
take damage, or return to kennels. Their leg movement is a procedural gait over
the idle, rather than a separate authored run clip.

## How I checked it

Tests damage real museum glass, wait for the staggered pack, check movement and
arrival, break all seven cases, verify the same three dogs remain, and reload the
scene to check cleanup. Presentation, door, and hammer/glass tests also run. All
20 Play Mode tests passed on 19 October 2026.
