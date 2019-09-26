using System;

namespace DemystifyExceptions.Demystify
{
    internal sealed class CacheDictionary<TKey, TValue> where TKey : class where TValue : class
    {
        public CacheDictionary(int cacheSize = 256)
#if APKD_STACKTRACE_NOCACHE
        {
        }
#else
            => (this.cacheSize, objectReferenceQueue) = (cacheSize, new Queue<object>(cacheSize));
#endif

        public TValue GetOrInitializeValue(TKey key, Func<TKey, TValue> initializer)
        {
#if APKD_STACKTRACE_NOCACHE
            return initializer(key);
#else
            if (!weakTable.TryGetValue(key, out var value))
                weakTable.Add(key, value = initializer(key));

            if (objectReferenceQueue.Count > cacheSize)
                objectReferenceQueue.Dequeue();
            objectReferenceQueue.Enqueue(value);

            return value;
#endif
        }
#if !APKD_STACKTRACE_NOCACHE
        private readonly int cacheSize;
        private readonly Queue<object> objectReferenceQueue;

        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TKey, TValue> weakTable
            = new System.Runtime.CompilerServices.ConditionalWeakTable<TKey, TValue>();

        private const int defaultCacheSize = 256;
#endif
    }
}