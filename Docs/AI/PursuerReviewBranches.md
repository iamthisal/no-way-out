# Pursuer review branches

The 18 existing pursuer commits are divided into named, stacked review branches. Original commits and dates are preserved. `Ai/pursuer-completion` remains the complete working branch; `Ai/pursuer-search-fixes` includes the same final implementation plus the review-guide commits.

## Review order

Compare each branch against the base listed below. Comparing every branch directly against `develop` would repeat its dependencies. For stacked PRs, the first base is `develop`, and each later base is its preceding review branch. Merge in order; the final destination is `develop`. Once a predecessor is merged, retarget its successor as appropriate.

| Review branch | Compare / PR base | Scope |
|---|---|---|
| [`Ai/pursuer-foundation`](https://github.com/iamthisal/no-way-out/compare/develop...Ai%2Fpursuer-foundation) | `develop` | Character assets, import tooling and configurable state foundation. |
| [`Ai/pursuer-perception`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-foundation...Ai%2Fpursuer-perception) | `Ai/pursuer-foundation` | Sight, hearing inputs and ally alerts. |
| [`Ai/pursuer-investigation-navigation`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-perception...Ai%2Fpursuer-investigation-navigation) | `Ai/pursuer-perception` | Clue investigation, complete routes and physical movement blockers. |
| [`Ai/pursuer-search`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-investigation-navigation...Ai%2Fpursuer-search) | `Ai/pursuer-investigation-navigation` | Bounded local search candidates. |
| [`Ai/pursuer-combat`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-search...Ai%2Fpursuer-combat) | `Ai/pursuer-search` | Warned bursts, occlusion cancellation and controlled weapon execution. |
| [`Ai/pursuer-formation-presentation`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-combat...Ai%2Fpursuer-formation-presentation) | `Ai/pursuer-combat` | Reachable flank slots, imported animations and combat feedback. |
| [`Ai/pursuer-museum-integration`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-formation-presentation...Ai%2Fpursuer-museum-integration) | `Ai/pursuer-formation-presentation` | Full FSM connection, scene/prefab setup, verification and legacy wanderer removal. |
| [`Ai/pursuer-runtime-separation`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-museum-integration...Ai%2Fpursuer-runtime-separation) | `Ai/pursuer-museum-integration` | Separate AI decisions from runtime movement, firing and presentation controls. |
| [`Ai/pursuer-arrest-interception`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-runtime-separation...Ai%2Fpursuer-arrest-interception) | `Ai/pursuer-runtime-separation` | Arrest/capture, route-based interception, attributed hits and advancing retaliation. |
| [`Ai/pursuer-search-fixes`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-arrest-interception...Ai%2Fpursuer-search-fixes) | `Ai/pursuer-arrest-interception` | Arrival-based inspection timers, persistent corner hypotheses and regression evidence. |

## Base and validation

The implementation starts at `748efb902c2f406efc1c12b4e1bcf76fe01bc0d4`, the original `develop` base. At branch creation on 2026-10-21 (Asia/Colombo), fetched `origin/develop` was `9ff0465`, eight newer team commits ahead of that base. Those changes have not been merged or rebased into this stack. Integrating them and resolving any conflicts is still required before declaring the final stack verified against current `develop`.

Early branches introduce supporting modules. The full FSM is wired into the museum in `Ai/pursuer-museum-integration`; early module branches do not individually represent the final encounter behavior. `Ai/pursuer-arrest-interception` and `Ai/pursuer-search-fixes` introduce and fix later arrest/interception behavior.

The complete implementation at `951aebc` passed 21 decision/component tests and 58 museum integration assertions. Creating branch references and this guide changes no Unity code or assets, so those results remain evidence for the original tested base, not the newer `develop` changes. General gunshot hearing remains unresolved; attributed bullet-hit retaliation is verified. Search remains timed, as documented in [Sentinel.md](Sentinel.md).

## History and working tree

No commits are cherry-picked, squashed, rebased or backdated. Review branches point to existing commit boundaries. The original `Ai/pursuer` and `Ai/pursuer-completion` branches are retained. Uncommitted project settings, recovery assets and the pre-existing stash are excluded from this split.

## Combined develop integration

`Ai/pursuer-develop-integration` brings the complete implementation together for one PR into `develop`. It merges `develop` at `9ff0465`, preserving the original commits and the separate review branches above.

The museum scene conflict was resolved by retaining the current lighting objects and all local scene references, keeping the pursuer/route/signal setup, and preserving the intentional removal of the legacy wandering enemy. The incoming Hound noise scripts are retained unchanged. Separate integration verification results are recorded in `Docs/Characters/PursuerDevelopIntegration.txt` and `Docs/Characters/PursuerDevelopDecisionTests.xml` when available.

The existing skeleton-archer and canopic-mummy PRs are outside this integration. They are not merged or otherwise changed by the pursuer PR.

