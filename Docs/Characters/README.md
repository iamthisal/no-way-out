# Pursuer verification artifacts

- `PursuerArrest.png`, `PursuerIntercept.png`, `PursuerAimWarning.png`, `PursuerAttack.png`, `PursuerDeath.png`: current gameplay character captured from the museum player camera during arrest-first encounter verification. Camera captures exclude the OnGUI arrest/status HUD; the arrest image is not evidence of a bespoke handcuff animation.
- `PursuerAIVerification.txt`: 48 full-encounter assertions covering actual route interception, arrest/escape, a real pistol hit from behind, retaliation, forward cover expiry, capture and restart, plus recorded decision marker timings.
- `PursuerDecisionTests.xml`: 16 NUnit tests for perception, attack timing, investigation, search, formations, arrest timing, observed-motion prediction, route selection and forward cover.
- `PursuerWandererRegression.xml`: four passing legacy combat tests after removing the museum wanderer. These instantiate test-only prefab fixtures and isolate the pursuer; the saved museum contains no wandering enemy.

Earlier character-development evidence is retained: `PursuerPreview.png` and `PursuerValidation.txt` concern the presentation-only import; `PursuerInGame.png` and `PursuerPlayModeValidation.json` show initial scene placement; `PursuerAIInGame.png` shows the earlier patrol/chase integration. Those files predate arrest-first behaviour.

The implementation, defaults, repeatable checks and remaining scope limits are documented in [the pursuer specification](../AI/Sentinel.md).
