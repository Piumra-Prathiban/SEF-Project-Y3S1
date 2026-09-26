# Member 4: AI Usage Log, What You Need and Where to Find It

The assignment requires an **individual AI usage log** and a **reflection** in the consolidated report.
This file does **not** contain a log. It lists exactly what each entry needs, where the *real* data lives, and
the few facts that can be stated reliably. Every entry must describe what actually happened; do not
reconstruct, round off, or backfill anything you cannot verify. If you did not use AI for a piece of work,
the log should not imply that you did.

## 1. What each log entry needs

| Field | What to record | Where the real data is |
|---|---|---|
| **Date** (and time if possible) | The day you ran the session | The tool's own history/transcript; not the commit date, which only shows when work *landed* |
| **Tool / model** | Product and exact model (for example "Claude Code, `claude-sonnet-5`") | The tool's settings, header or transcript |
| **Task** | What you asked for, in your own words, plus the prompt (paste or attach it) | Your chat history |
| **Output received** | What the AI produced: files, explanation, plan, code, numbers | The transcript; the diff it produced |
| **Changes made** | What *you* kept, edited, or wrote yourself, with file names and commit hashes | `git diff` / `git show <hash>`; be specific about what you changed by hand |
| **Rejected suggestions** | Suggestions you declined or reversed, and why | Only you know this; the transcript shows what was proposed |
| **Verification performed** | How you checked it: tests run (with the command and result), manual run, reading the code, comparing with docs | Test output, CI run, screenshots |

Good practice: one entry per meaningful session or task, written soon after it happens, with links to the
commit and the CI run.

## 2. Where the real records are

| Source | What it can prove | Limits |
|---|---|---|
| The AI tool's own history | Dates, prompts, outputs | You must export/copy it yourself |
| Local Claude Code transcripts: `C:\Users\hrind\.claude\projects\c--Users-hrind-OneDrive-Desktop-3y1s-SEF-SEF-grp-project-SEF-project\` | Full record of that session | **Only one transcript file exists in this folder**, last modified 2026-09-26 12:17 (this session). No earlier session files are there, so any tool use in Phases 1–11 is not recorded in that folder. If you used an AI tool for those phases, look in that tool's own history |
| Git commits | When work landed and exactly what changed | Does not say whether AI was involved. None of your 12 commits has a `Co-Authored-By` AI trailer |
| GitHub Actions / test output | That the result was verified | Shows the outcome, not who wrote the code |
| Editor local history, terminal history | Timeline of edits and commands | Incomplete |

Your commit times (all +05:30): Phase 1 on 09-23 13:07; Phases 3–8 on 09-24 between 18:47 and 23:20; Phases 9–11 on
09-24 23:44 → 09-25 00:26; Phase 12 on 09-26 10:35; Phase 13 on 09-26 12:06. These bound *when work was
committed*. They are not AI-usage dates.

## 3. Facts about the Phase 12–14 session (from the conversation record; confirm before using)

These are drawn from this conversation and Git. They are the only entries that can be stated reliably from
here. Fill the last column yourself. Nothing is pre-filled for it.

| Date | Tool / model | Task (from the prompt) | Output received | Changes made (verifiable) | Verification (verifiable) | Rejected suggestions |
|---|---|---|---|---|---|---|
| 2026-09-26 | Claude Code, `claude-sonnet-5` | **Phase 12:** comprehensive testing of the Marketing component across backend, database, React, Flutter, agent, end to end | New tests; a fix to the test factory; a written report. Flutter could not be run (no SDK, disk full) | Commit `fa8fea5`: `InventoryPromotionAgentApprovalAuthorizationTests`, `PromotionServiceTransactionTests`, `MarketingApiFactory` fix, two React campaign test files (5 files, +607/−10) | Backend 324/324, React 131/131 run locally; migration and constraints checked on real PostgreSQL; CI green | *(yours)* |
| 2026-09-26 | same | **Phase 13:** security and performance review | Findings and fixes: approval race, unbounded page number, unbounded ID lists, two slow analytics queries; new security test suite; benchmark tables | Commit `84accb7` (13 files, +668/−61) | Backend 439/439; React 131/131; Flutter 38/38 (first real run); performance measured on a scratch PostgreSQL database with 200k orders (results in the technical doc); a mutation check showed the new tests fail when the fix is removed; CI green | *(yours)* |
| 2026-09-26 | same | **Phase 14:** documentation, contribution evidence, this guide, viva list | The four `docs/MEMBER4_*.md` files | Files created in `docs/`; **not committed** at the time of writing | Claims checked against `git log`, the GitHub REST API (PRs, issues, comments, Actions) and the source; two inaccuracies in older docs were found and recorded | *(yours)* |

Points about that session that the transcript shows and that belong in an honest log:

* **The assistant discarded some of its own attempts** (recorded in the transcript): a first analytics rewrite
  that EF translated back into correlated subqueries (slower), and a candidate index on `Orders.PlacedAt` that
  measured no benefit. These are the assistant's discarded attempts, **not** suggestions you rejected.
* **The assistant made a mistake to disclose:** while stopping a hung helper process it ended `python.exe`
  and `python3.exe` by process name, which could have stopped an unrelated Python program.
* The assistant installed a Flutter SDK into a temporary folder to run the tests and deleted it afterwards.
  It restored the `pubspec.lock` that `flutter pub get` had modified.
* The assistant did **not** commit or push anything; the two commits above were made by you.

If your own recollection differs from anything above (for example you edited the output by hand, or declined
something), your log should say so. The transcript, not this file, is the authority.

## 4. Blank entry template

| Date/time | Tool / model | Task and prompt | Output received | Changes I made (files, commits) | Suggestions I rejected and why | How I verified it |
|---|---|---|---|---|---|---|
| | | | | | | |

## 5. Reflection outline (the consolidated report needs your own words)

Write these yourself; specifics beat generalities.

1. **How you used AI**: which tasks, and how much of each task's result you understood before accepting it.
2. **What it got wrong** and how you found out (tests, reading the code, CI, a teammate). Concrete examples
   are more convincing than "AI can make mistakes".
3. **What you changed or rejected**, and why.
4. **How you verified** correctness and security, and what you would not trust AI output for without checking.
5. **Ownership**: which parts you can explain and defend at the viva without the tool, and which you cannot yet
   (be honest, then use the viva list to close the gaps).
6. **Ethics and attribution**: how the log and commit messages make AI involvement transparent.
7. **What you would do differently** next time.
