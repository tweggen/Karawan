namespace engine;

public interface IAssetImplementation
{
    public System.IO.Stream Open(in string filename);
}