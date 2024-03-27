namespace engine;

public interface IAssetImplementation
{
    public System.IO.Stream Open(in string filename);
    public void AddAssociation(string tag, string uri);
}