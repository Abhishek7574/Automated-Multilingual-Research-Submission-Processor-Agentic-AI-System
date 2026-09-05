using BackEnd.Models;
using System.Collections.Concurrent;

namespace BackEnd.Storage
{
    public class VectorStore
    {
        private readonly ConcurrentDictionary<string, List<VectorChunk>> _vectors = new();

        public void AddChunk(string documentId, VectorChunk chunk)
        {
            var list = _vectors.GetOrAdd(documentId, _ => new List<VectorChunk>());

            lock (list)
            {
                list.Add(chunk);
            }
        }

        public IReadOnlyList<VectorChunk> GetChunks(string documentId)
        {
            return _vectors.TryGetValue(documentId, out var list)
                ? list
                : new List<VectorChunk>();
        }

    }
}
