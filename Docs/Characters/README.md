# Pursuer verification artifacts

- `PursuerAimWarning.png`, `PursuerAttack.png`, `PursuerDeath.png`: current gameplay character, captured from the actual museum player camera during automated verification.
- `PursuerAIVerification.txt`: 27 full-encounter integration assertions and the recorded decision marker timings.
- `PursuerDecisionTests.xml`: nine NUnit tests for perception, attack timing, investigation, search and formation behavior.
- `PursuerWandererRegression.xml`: two existing autonomous wandering-guard shooting tests, isolated from the new pursuer during those tests.

Earlier character-development evidence is retained: `PursuerPreview.png` and `PursuerValidation.txt` concern the presentation-only import; `PursuerInGame.png` and `PursuerPlayModeValidation.json` show initial scene placement; `PursuerAIInGame.png` shows the earlier patrol/chase integration. Those files do not describe the completed seven-state FSM.

The implementation, defaults, repeatable checks and remaining scope limits are documented in [the pursuer specification](../AI/Sentinel.md).
