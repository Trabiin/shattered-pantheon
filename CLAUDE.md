# Working in this repository

All work follows the project workflow in the shared folder: `/mnt/project-files/WORKFLOW.md`. Read it first. The short version for code:

- **Only backlog work.** Every change belongs to a backlog story (`/mnt/project-files/backlog/`). Its ID (for example `E7-F3-S2`) starts the branch name, every commit message and the pull request title.
- **One branch per story**, from the latest `main`. Never commit to `main` directly. Ojon merges pull requests.
- **Tested before review.** All GitHub checks green. New behaviour gets automatic tests. Changes to battle rules or data re-run `tools/BattleSim`; changes to rewards, costs or campaign re-run `tools/ProgressionSim`; both commit their reports. Never skip, switch off or loosen a test.
- **Generated data is never edited by hand.** `Unity/Assets/Resources/BattleData/` is written by `tools/BattleData/`.
- **Docs in the same change.** The design docs in `/mnt/project-files/plan/`, this repository's `README.md` and `tools/BattleData/README.md` must describe what `main` does once the pull request merges. Replace old text; don't leave it beside the new.
- **No clutter.** No commented-out code, unused files, debug output, "v2" copies, or TODOs without a backlog ID (`TODO(E7-F3-S2-T4)`). Delete, don't archive: git history keeps the past.
- **After a merge:** the branch is deleted, the backlog story is marked done, and the shared-folder report copies are refreshed (`reports/latest.md` goes to `/mnt/project-files/reports/battle-balance.md`, `reports/progression.md` to `/mnt/project-files/reports/progression.md`).
