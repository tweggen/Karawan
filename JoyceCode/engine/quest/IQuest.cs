namespace engine.quest;

public interface IQuest
{
    engine.world.IWorldOperator CreateQuestWorldOperator();
}