# Pursuer review branches

The 18 existing pursuer commits are divided into ten stacked review branches. Original commits and dates are preserved. `Ai/pursuer-completion` remains the complete working branch; the tenth review branch includes the same final implementation plus this guide.

## Review order

Compare each branch against the base listed below. Comparing every branch directly against `develop` would repeat its dependencies. For stacked PRs, the first base is `develop`, and each later base is its preceding review branch. Merge in order; the final destination is `develop`. Once a predecessor is merged, retarget its successor as appropriate.

| Order | Review branch | Compare / PR base | Existing commits | Scope |
|---|---|---|---|---|
| 1 | [`Ai/pursuer-01-foundation`](https://github.com/iamthisal/no-way-out/compare/develop...Ai%2Fpursuer-01-foundation) | `develop` | 1 | Character assets, import tooling and configurable state foundation. |
| 2 | [`Ai/pursuer-02-perception`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-01-foundation...Ai%2Fpursuer-02-perception) | `Ai/pursuer-01-foundation` | 1 | Sight, hearing inputs and ally alerts. |
| 3 | [`Ai/pursuer-03-investigation-navigation`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-02-perception...Ai%2Fpursuer-03-investigation-navigation) | `Ai/pursuer-02-perception` | 2 | Clue investigation, complete routes and physical movement blockers. |
| 4 | [`Ai/pursuer-04-search`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-03-investigation-navigation...Ai%2Fpursuer-04-search) | `Ai/pursuer-03-investigation-navigation` | 1 | Bounded local search candidates. |
| 5 | [`Ai/pursuer-05-combat`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-04-search...Ai%2Fpursuer-05-combat) | `Ai/pursuer-04-search` | 2 | Warned bursts, occlusion cancellation and controlled weapon execution. |
| 6 | [`Ai/pursuer-06-formation-presentation`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-05-combat...Ai%2Fpursuer-06-formation-presentation) | `Ai/pursuer-05-combat` | 2 | Reachable flank slots, imported animations and combat feedback. |
| 7 | [`Ai/pursuer-07-museum-integration`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-06-formation-presentation...Ai%2Fpursuer-07-museum-integration) | `Ai/pursuer-06-formation-presentation` | 2 | Full FSM connection, scene/prefab setup, verification and legacy wanderer removal. |
| 8 | [`Ai/pursuer-08-runtime-separation`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-07-museum-integration...Ai%2Fpursuer-08-runtime-separation) | `Ai/pursuer-07-museum-integration` | 1 | Separate AI decisions from runtime movement, firing and presentation controls. |
| 9 | [`Ai/pursuer-09-arrest-interception`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-08-runtime-separation...Ai%2Fpursuer-09-arrest-interception) | `Ai/pursuer-08-runtime-separation` | 3 | Arrest/capture, route-based interception, attributed hits and advancing retaliation. |
| 10 | [`Ai/pursuer-10-search-fixes`](https://github.com/iamthisal/no-way-out/compare/Ai%2Fpursuer-09-arrest-interception...Ai%2Fpursuer-10-search-fixes) | `Ai/pursuer-09-arrest-interception` | 3 + this guide | Arrival-based inspection timers, persistent corner hypotheses and regression evidence. |

## Base and validation

The implementation starts at `748efb902c2f406efc1c12b4e1bcf76fe01bc0d4`, the original `develop` base. At branch creation on 2026-10-21 (Asia/Colombo), fetched `origin/develop` was `9ff0465`, eight newer team commits ahead of that base. Those changes have not been merged or rebased into this stack. Integrating them and resolving any conflicts is still required before declaring the final stack verified against current `develop`.

Early branches introduce supporting modules. The full FSM is wired into the museum in branch 07; early module branches do not individually represent the final encounter behavior. Branches 09 and 10 introduce and fix later arrest/interception behavior.

The complete implementation at `951aebc` passed 21 decision/component tests and 58 museum integration assertions. Creating branch references and this guide changes no Unity code or assets, so those results remain evidence for the original tested base, not the newer `develop` changes. General gunshot hearing remains unresolved; attributed bullet-hit retaliation is verified. Search remains timed, as documented in [Sentinel.md](Sentinel.md).

## History and working tree

No commits are cherry-picked, squashed, rebased or backdated. Review branches point to existing commit boundaries. The original `Ai/pursuer` and `Ai/pursuer-completion` branches are retained. Uncommitted project settings, recovery assets and the pre-existing stash are excluded from this split.

This task creates and publishes review branches only; it does not create PRs or merge anything.

