### 2026-11-22 — KAR-493: a walking NPC talked to twice walks on afterwards

- **Reported by the owner:** a walking worker, talked to several times with E, stopped at the first conversation and never walked again.
- **Two causes:**
  1. `TaleWalkBehavior._pauseWalking` saved `Navigator.Speed` as the speed to resume with on every E press. On the second press it was already 0, so every later resume set 0.
  2. The second E ends the running conversation before it starts the new one. That end (`ScriptEndedEvent`, same script name) is queued and reached the new `ConversationTimeout`, which ended the new conversation at once.
- **Fix:**
  - The speed is saved only on the first pause.
  - `ConversationTimeout` counts a `ScriptEnded` only after its own `ScriptStarted`. A conversation that does not start within 2 s (the narration refused it) ends the watch, so the NPC resumes instead of waiting.
- **Tests:** TestRunner `./run_tests.sh bugfix` (+2, 11/11): a replaced conversation's end does not end its successor; a walker talked to twice resumes at its walking speed. Both fail with the fixes reverted, the second with "resumed at 0". xUnit 2305 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
