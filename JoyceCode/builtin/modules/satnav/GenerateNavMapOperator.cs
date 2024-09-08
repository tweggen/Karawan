using System.Threading.Tasks;
using builtin.modules.satnav.desc;
using engine;
using static engine.Logger;


namespace builtin.modules.satnav;

public class GenerateNavMapOperator : engine.world.IWorldOperator
{
    public string WorldOperatorGetPath()
    {
        return "builtin.modules.satnav/GenerateNavMapOperator";
    }


    public System.Func<Task> WorldOperatorApply() => new (async () =>
    {
        NavCluster ncTop = new()
        {
            Id = "Top"
        };
        
        I.Get<NavMap>().TopCluster = ncTop;
        

        Trace("GenerateNavMapOperator: Done.");
    });
    

    public GenerateNavMapOperator()
    {
    }
}