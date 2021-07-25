namespace Attractor
{
    public interface IMetadata
    {
        T GetFeature<T>() where T : class;

        void SetFeature<T>(T feature) where T : class;
    }
}
