### 2026-11-19 — KAR-476: talking to a walking TALE NPC counts

- **Report (Windows playtest):** after greeting NPCs with E, the TALE panel still showed 0 NPCs known to the player, and the log had neither `TALE: chat with NPC …` nor `… ended without chat`.
- **Cause:** `TaleWalkBehavior.OnAction` triggered the conversation without `TaleNarrationBindings.BeginSession`. `TaleModule._onScriptEnded` found no session and returned silently, so no chat was applied, no `Trust[-1]` was written and nothing was logged. S1-c (KAR-456) added the session to `TaleConversationBehavior` (standing NPCs) only. Walking NPCs, most of the city, never registered a conversation. A hit or ram mid-dialogue on a walking NPC also bypassed `EndByViolence`.
- **Fix:**
  - `TaleWalkBehavior` begins the session before triggering the script.
  - A hit or crash goes through `EndByViolence` first and falls back to a plain cancel when the NPC isn't the session's partner.
- **Tests:**
  - `ConversationSessionSourceTests`: every `TriggerConversation` in both TALE behaviours is preceded by its own `BeginSession`, and the walking behaviour ends a hit as violence. Without the fix, 2 of the 3 fail.
  - xUnit 2227 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
- **Pending in-game check:** a finished talk with a walking NPC logs `TALE: chat with NPC` and the panel counts the NPC as known.
