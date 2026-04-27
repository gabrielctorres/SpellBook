namespace SpellBook.Systems.Pooling
{
    /// <summary>
    /// Interface para objetos gerenciados por um ComponentPooler local.
    /// Permite que os objetos resetem seu estado sem Destroy/Instantiate.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawnFromPool();
        void OnReturnToPool();
    }
}

