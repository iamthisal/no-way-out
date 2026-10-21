# Pursuer verification artifacts

The combined `Ai/pursuer-develop-integration` checkout includes `develop` at `9ff0465` and the high-frame-rate observation fix. [PursuerDevelopIntegration.txt](PursuerDevelopIntegration.txt) records 58 passing museum encounter assertions in Unity 6000.6.3f1 batch mode. [PursuerDevelopDecisionTests.xml](PursuerDevelopDecisionTests.xml) records 22 passing EditMode decision/component tests, including rapid-frame velocity sampling. Verification requests 60 FPS and restores the original timing settings afterward; its startup uses an editor update callback to avoid interference from the editor's SearchDatabase startup exception. Earlier artifacts below remain evidence for their original snapshots.

- `PursuerArrest.png`, `PursuerIntercept.png`, `PursuerAimWarning.png`, `PursuerAttack.png`, `PursuerDeath.png`: current gameplay character captured from the museum player camera during arrest-first encounter verification. Camera captures exclude the OnGUI arrest/status HUD; the arrest image is not evidence of a bespoke handcuff animation.
- `PursuerAIVerification.txt`: 58 full-encounter assertions covering a selected occluded museum corner, actual arrival and persistent inspection, hidden-position isolation, eventual give-up, route interception, arrest/escape, a real pistol hit from behind, retaliation, forward cover expiry, capture and restart, plus decision marker timings.
- `PursuerDecisionTests.xml`: 21 NUnit tests for perception, attack timing, investigation, bounded search, arrival-based inspection timing, ranked corner hypotheses, formations, arrest, observed-motion prediction, route selection and forward cover.
- `PursuerWandererRegression.xml`: four passing legacy combat tests after removing the museum wanderer. These instantiate test-only prefab fixtures and isolate the pursuer; the saved museum contains no wandering enemy.

Earlier character-development evidence is retained: `PursuerPreview.png` and `PursuerValidation.txt` concern the presentation-only import; `PursuerInGame.png` and `PursuerPlayModeValidation.json` show initial scene placement; `PursuerAIInGame.png` shows the earlier patrol/chase integration. Those files predate arrest-first behaviour.

The implementation, defaults, repeatable checks and remaining scope limits are documented in [the pursuer specification](../AI/Sentinel.md).
