# Sprint 8 Retrospective - Audit Trail + Enrollment Model

**Sprint window:** 2026-11-20 (one day, three sessions)
**Demo:** admin/students -> open a student -> Exit; admin/edfi/sync -> Preview -> Run;
admin/audit shows who did each step. The Ed-Fi enrollment carries the exit date.
**Goal:** every view and change of a student is in an append-only audit trail (ADR-004), and
the Ed-Fi sync sends the enrollments Phoenix holds instead of one configured entry date.

## Stories completed

| Story | Commit | Result |
|---|---|---|
| PHX-8.1 Audit trail core | 13b6c7d | `IAuditLog`, `AuditEvent` + append-only trigger, actor from Entra, identity writes audited |
| PHX-8.2 Enrollment model | 01cf2f1 | `Enrollment` table, campus directory from config, rules, enroll / exit / bulk audited |
| PHX-8.3 Student pages | 4646ae2 | `Students` flag, list + record pages, enroll, exit, bulk enroll, audited reads |
| PHX-8.4 Sync from enrollments | d9c4549, 3f7f43f | One SSA per Phoenix enrollment, exit sent, unenrolled skipped, preview / apply audited with a result row |
| PHX-8.5 Audit viewer + close | 46ca41b, 0777571 | `admin/audit`, filters, `AuditView` recorded, pages load once |

Tests: 73 at the end of 8.3 -> 88 at close (3 architecture + 85 infrastructure). Build: 0 warnings.

## Live verification (2026-11-20, sample data only)

- Phoenix: 19 students, 19 enrollments at 220822001 for SY 2027 (entry 2026-08-17), 19 Ed-Fi
  ids PHX-000001..19. PHX-000001 exited 2026-11-20, reason Withdrew.
- Sync run: 13 updated, 6 created (the Flightdeck sample), 0 failed, 0 skipped.
- Ed-Fi GET: 19 StudentSchoolAssociations at 220822001; PHX-000001 has exitWithdrawDate
  2026-11-20, type Withdrawn; the other 18 are open; 19 LEA associations and 19 Local
  identification codes at 220822; no SSN.
- Audit: 283 rows. Every student has two SyncApply rows for the run (send started, then
  created / updated). One list visit = 19 View rows, written once. No summary contains a
  student's first or last name. Actor is the signed-in Entra user on every row.
- Trigger: UPDATE and DELETE on `AuditEvent` each rejected with "AuditEvent is append-only
  (ADR-004): UPDATE and DELETE are not allowed."; row count and content checksum identical
  before and after. Run as the application SQL login.

## 1. What went well?

- Probing before writing: the three descriptor sets were read from the live API before the
  mapping was coded, so the first live run had no descriptor failures.
- The audit rows themselves found a bug. Two list visits had written 38 rows for 19 students
  in the same second; that led straight to the prerender double load.
- Fail closed held up in tests and live: no data is shown or sent when the audit write fails.

## 2. What was harder than expected?

- "Audit before the send" and "audit what happened" are two different rows. The first cut
  wrote "sent to Ed-Fi" before sending. It is now "send started" plus a result row.
- InteractiveServer pages run their load twice. Every audited read page needs the
  `RendererInfo.IsInteractive` guard, or one visit is recorded as two.
- The correlation id is per browser session, not per action. The viewer first labelled it
  "Operation".

## 3. What did I learn?

- Ed-Fi keeps exited students visible to the client (two 2022 exits in the sandbox are still
  readable), and the LEA association and identification code writes succeed after the exit.
- Core Ed-Fi has no exit type for non-attendance. "No show" means never attended.
- A secret check that greps for the word "secret" proves nothing. Grep for the values.
- The bulk-enroll form must not guess an entry date: Ed-Fi keys the enrollment on it and
  Phoenix never lets it change.

## 4. What will I do differently next sprint?

- Add the interactive-only load guard to any new page that reads students, and check the
  audit rows after the first visit.
- Run the value-based secret check after `git add`, every commit.
- After any change to the sync, run it twice live: the second run exercises the update path.

## 5. Open items / follow-ups

- **Not yet proven:** updating a student whose only enrollment is exited. In the verified run
  the student record for PHX-000001 was written before the exit. The next sync Run proves it:
  PHX-000001 should be "Updated". A 403 on students means the sync needs a rule for exited
  students.
- `ExitReason.NonAttendance` is sent as Withdrawn, the same as Withdrew. Decision with Hailee
  (`docs\features\modules\EdFi.md`).
- If the result rows cannot be written after a send, the sync warns and logs instead of
  failing. Confirm warn or throw.
- 38 of the View rows from before the fix are duplicates. They stay; the table is append-only.
- The application SQL login is `db_owner` and could disable the trigger. Separate it from the
  schema owner before real data loads (ADR-004).
- Database backups for PhoenixSIS and EdFi_* on the SQL VM were never confirmed.
- Audit retention period: New Heights to set.
- Correcting an entry date after sync (Ed-Fi delete + re-create) is not built.
