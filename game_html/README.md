# Warehouse Lockdown

Run `npm install`, then `npm run dev` and open http://127.0.0.1:4173/.
For a production build, run `npm run build`, then `npm run preview`.

WASD moves, the mouse aims, and the left mouse button fires the blaster.
Hold Shift to sprint, C to crouch, and press E to use a nearby lever.
Q calls Rex for a distraction. Escape pauses; Resume captures the mouse again.

The safe start room leads clockwise through three combat rooms. Use the first
lever to open Door 1. Clear three waves in each combat room to unlock its lever.
The final lever appears after Room 3 is cleared and shows You Won with Restart.
Shots disable robots but attract nearby security. The flashlight follows your aim.

Blue Sentinels pursue you and fire red bolts. Purple Stalkers peek from cover,
shoot, and retreat. Their aiming beams warn you before firing; move sideways or
duck behind shelves to avoid the bolts. Yellow Wasps patrol and raise the alarm.

Destroyed robots stay dead; only the next wave creates new enemies. Enemies,
patrols, sounds, and navigation are restricted to their own room. Closed doors
block movement, shots, and sight. Hold Shift while moving for a faster sprint;
sprinting consumes stamina, which recharges while walking or standing still.

Security coordinates its attacks: one Sentinel presses forward while the other
flanks, and they strafe and fire aimed bursts. Losing sight triggers a fourteen
second search around the last confirmed location. Stalkers reserve different
cover positions and relocate when hit or when they lose their firing angle.
Enemy bolts lead visible movement, but aiming beams lock shortly before firing,
so changing direction and breaking line of sight remain useful.

Purple Stalkers are guerrilla fighters: they spot exposed players up to 48 units
away, choose concealed cover with a clear firing angle, fire a two-shot burst,
and immediately hide. Attack and hiding timers start on arrival at their post.
Approaching within 7 units, rushing them from within 13 units before they commit
to a burst, or hitting them makes them run to another covered position. They
finish committed bursts unless you get dangerously close. When no cover route
is available they use a reachable escape point, and stalled routes are replanned.
They retreat at 7.7 units per second;
your Shift sprint is still faster. Shelves and walls block their sight and shots.

Press Q once to send Rex to a reachable distraction location. A cyan beacon
marks the distraction; nearby robots investigate it if they cannot
currently see you. Break line of sight first to divert a pursuer. Rex returns
to follow you after staying at the destination for six full seconds. Travel
time does not consume that duration. The ability becomes ready again after sixteen
seconds. Holding Q does not automatically trigger the next distraction.

## Difficulty and checks

Tune all room wave mixes and counts, pauses, spawn/patrol locations, bounds,
and atmosphere in `src/room-config.js`. Room 1 contains Sentinels; Room 2
contains mostly Stalkers; Room 3 adds Wasps and the existing sound-tracking Hound.
Room controllers manage spawning and unlocks independently of enemy AI.

Health recovers at 6 HP per second after five seconds without damage. Take cover
to recover during combat. Clearing a wave restores 25 HP; finishing a room
refills health. Recovery stops on damage, never exceeds 100 HP, and pauses with
the game. Tune these values in `RECOVERY_CONFIG` in `src/room-config.js`.

To test manually: wait in the start room (no enemies), try walking/shooting
through Door 1, approach its lever and press E, then cross into Room 1. Check
that the HUD shows Wave 1/3 and two Sentinels. Try the next lever before clearing
the room: it should say Locked: clear the room. Clear each wave and observe the
short pause and rising counts. After wave 3, press E on the lever. Repeat in
Rooms 2 and 3; check blue, purple, and red atmosphere changes. Clear the last
wave, use the final lever, and verify You Won and Restart. Escape pauses waves.

`node scripts/room-controller-test.js` checks lifecycle and pause behavior.
With a Chromium debugging endpoint on port 9222 and the dev server running,
`node scripts/room-flow-test.cjs` checks the full route, collision, shots,
room containment, victory, and desktop/mobile rendering.
