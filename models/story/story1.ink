-> Main

EXTERNAL triggerQuest(questName)

=== function triggerQuest(questName) ===
~ return ""

== Main ==

Again I looked around. This was all? This was it?
Rumours are this is a vast space. Rumours are this is inside something.
They say we run the world that is outside of us.
For now, I need something that runs the inside of me.

 + [ Find a Ramen shop ] -> ramen1
 + [ Go get a drink. ] -> drink1

-> END
    
== ramen1 ==

Where can I get some hot and tasty ramen here?
I guess the navigation should display.
{ triggerQuest("nogame.quests.VisitAgentTwelve.Quest") }

-> END // continues with agent12 after we reached the target.

== drink1 ==

might be early or late, you never know, surely it's dim. A drink wouldn't hurt.
Let's see if navigation can help us.
{ triggerQuest("nogame.quests.VisitAgentTwelve.Quest") }

-> END // continues with agent12 after we reached the target.

=== agent12 ===

I can't see agent 12. He usually would be here if I long for a beverage or a meal.
He would tell me something, you know, get straight to it. 
Sometimes I wonder if he knows the fishmongers. Though I never dared to ask him.
{ triggerQuest("nogame.quests.HelloFishmonger.Quest") }

-> END

=== firstPubSecEncounter ===

Well, I hit them, but they just rode on. Guess I have to repair my car.
Driving into any arbitrary polytope will do it.
However, I hope public security didn't notice the bump.
{ triggerQuest("nogame.quests.FirstPubSec.Quest") }
{ triggerQuest("nogame.quests.FirstReacharge.Quest") }

-> END // continues with HelloFishmonger after we reached the target.


