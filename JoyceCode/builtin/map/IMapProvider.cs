
namespace builtin.map;

public interface IMapProvider : IWorldMapProvider, IFragmentMapProvider
{
    public void AddWorldMapLayer(string layerKey, IWorldMapProvider worldMapProvider);
}