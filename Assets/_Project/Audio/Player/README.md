# Player Hit And Death Sounds

`ClassicOof.ogg` is a copy of the classic Roblox "oof" sound downloaded from:
https://github.com/CrispyDesires/Revert-Roblox-Death-Sound/blob/main/ouch.ogg

Downloaded on 2026-10-11 for the requested prototype hit feedback.
This is a third-party recording, not an original project asset. No license
grant for distributing it in a standalone Unity game has been verified.
Obtain appropriate permission or replace the clip before public distribution.

The player prefab assigns it to PlayerVitals.hitSound. PlayerVitals.hitVolume
controls playback loudness. It plays on accepted nonfatal damage, but not on
misses, blocked shots, invalid damage, or further hits after death.

## Death Scream

`WilhelmDeathScream.ogg` is the Wilhelm scream, downloaded unchanged on
2026-10-11 from:
https://upload.wikimedia.org/wikipedia/commons/d/d9/Wilhelm_Scream.ogg

Source and license declaration:
https://commons.wikimedia.org/wiki/File:Wilhelm_Scream.ogg
The source page lists CC0 1.0 Universal Public Domain Dedication:
https://creativecommons.org/publicdomain/zero/1.0/
The page attributes the original voice as likely Sheb Wooley and credits
Ruxnor for the trimmed upload from the USC optical sound effects archive.
This is not a Tom and Jerry or Disney Goofy recording.

PlayerVitals.deathSound references this clip; deathVolume controls loudness.
On death, existing player hit audio stops and this non-looping scream plays
once. The fatal hit does not also play oof. Reloading the scene destroys the
old player and its audio source.
