# Phase C Tests: NPC Conversation System

> **Status:** the 29 scenario scripts are removed; what can be checked headless is in xUnit ·
> **Changed:** 2026-10-20 (KAR-433, TALE S0 W1)

The conversation system (C1–C4, `../../plans/done/TALE-NPC-CONVERSATION-SYSTEM-C1-C4.md`)
runs only in the game: `TaleConversationBehavior` on a materialised NPC, narration scripts, the
player pressing E. The TestRunner world is a DES of schedules with none of that, and no code
emits the `dialogue_started` event these scripts expected. They declared their checks in an
`"expected"` block `ScriptRunner` ignored, so they passed without running; they are removed
(their directories `phaseC1-infrastructure` … `phaseC4-trust` with them).

## Checked in xUnit (`tests/JoyceCode.Tests/engine/tale/ConversationRulesTests.cs`)

The pure rules moved out of nogame into JoyceCode so they can be tested; nogame calls them.

| Old script(s) | Rule | Code |
|---|---|---|
| C1 06, C1 07 | no storylet or tag script → `tale.role.{role}` | `ConversationScriptResolver` |
| C2 01, C2 04 | `conversation_script` override wins over id, tag and role | same |
| C2 02, C2 03, C2 05 | the first tag with a `tale.tag.{tag}` script | same |
| — | `tale.{storyletId}` before tags; `tale.generic` last | same |
| C3 01–04 | `npcMood()` / `npcWealthLabel()` thresholds and order | `NpcMoodText` |
| C4 01, C4 02 | first contact 0.15 (S1-a); a finished conversation applies the shipped `chat` (+0.02 × (1 − trust), S1-c), capped at 1, contact marked | `PlayerConversation.OnEnded` → `TaleManager.ApplyPlayerInteraction` |
| C4 03, C4 04 (gates only) | the merchant's `returning` gate (0.19) opens after 3 conversations an hour apart, the authority's `trusted` (0.27) after 8 | `RoleScriptTrustGates_OpenAfterTheSameNumberOfConversations` |
| S1-c | violence on the partner ends the conversation without chat (assault applied); another script ending applies nothing; `tale.interact` / `tale.pay` replace the chat; pay needs the cash | `PlayerConversationTests` |
| C4 06 | `tale.npc.remember` stores `player_fact.{fact}` | `PlayerConversation.Remember` |

`TaleModule` still has to *call* `PlayerConversation.OnEnded` on `ScriptEndedEvent`, and
`TaleConversationBehavior` / `TaleEntityStrategy` `TaleNarrationBindings.EndByViolence` on a
hit; that wiring is not tested (in-game check: a punch closes the dialogue).

## Not verified anywhere

These need the running game and are checked by hand only:

- C1 01, 08: the behaviour attaches to outdoor NPCs and not to indoor ones.
- C1 02–05, C2 06, C4 03–04: which branch a narration script takes for given properties or
  trust (the conditions live in the script JSON under `models/`).
- C3 05–06: text and speaker interpolation of `{func.npcMood()}` / `{func.npcRole()}`.
- C4 05: `met_player` survives save/load; C4 07: `quest.trigger` from dialogue; C4 08: the
  conversation timestamp; C4 09: several NPCs with independent trust.
