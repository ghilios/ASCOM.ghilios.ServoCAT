#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Interfaces;
using Nito.AsyncEx;
using System;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.ghilios.ServoCAT.Utility {

    public class MicroCacheFactory : IMicroCacheFactory {

        public IMicroCache<T> Create<T>() {
            return Create<T>(MemoryCache.Default);
        }

        public IMicroCache<T> Create<T>(ObjectCache objectCache) {
            return new MicroCache<T>(objectCache);
        }
    }

    /***
     * Cache with a configurable expiration policy, which ensures a get function is evaluated only once even when there are multiple concurrent readers.
     * Adapted from StackOverflow: https://stackoverflow.com/questions/32414054/cache-object-with-objectcache-in-net-with-expiry-time
     */

    public class MicroCache<T> : IMicroCache<T> {

        public MicroCache(ObjectCache objectCache) {
            if (objectCache == null)
                throw new ArgumentNullException("objectCache");

            this.cache = objectCache;
        }

        private readonly ObjectCache cache;
        private AsyncReaderWriterLock synclock = new AsyncReaderWriterLock();

        public async Task<bool> Contains(string key, CancellationToken ct) {
            using (await synclock.ReaderLockAsync(ct)) {
                return this.cache.Contains(key);
            }
        }

        public async Task<T> GetOrAddAsync(string key, Func<Task<T>> loadFunction, Func<CacheItemPolicy> getCacheItemPolicyFunction, CancellationToken ct) {
            LazyLock<T> lazy;
            bool success;

            using (await synclock.ReaderLockAsync(ct)) {
                success = this.TryGetValue(key, out lazy);
            }

            if (!success) {
                using (await synclock.WriterLockAsync(ct)) {
                    if (!this.TryGetValue(key, out lazy)) {
                        lazy = new LazyLock<T>();
                        var policy = getCacheItemPolicyFunction();
                        this.cache.Add(key, lazy, policy);
                    }
                }
            }

            return await lazy.GetAsync(loadFunction);
        }

        public T GetOrAdd(string key, Func<T> loadFunction, Func<CacheItemPolicy> getCacheItemPolicyFunction) {
            return AsyncContext.Run(() => GetOrAddAsync(key, () => Task.Run(loadFunction), getCacheItemPolicyFunction, CancellationToken.None));
        }

        public Task<T> GetOrAddAsync(string key, Func<Task<T>> loadFunction, TimeSpan timeToLive, CancellationToken ct) {
            return GetOrAddAsync(
                key,
                loadFunction,
                () => new CacheItemPolicy() {
                    Priority = CacheItemPriority.NotRemovable,
                    AbsoluteExpiration = DateTime.Now + timeToLive
                },
                ct);
        }

        public T GetOrAdd(string key, Func<T> loadFunction, TimeSpan timeToLive) {
            return GetOrAdd(
                key,
                loadFunction,
                () => new CacheItemPolicy() {
                    Priority = CacheItemPriority.NotRemovable,
                    AbsoluteExpiration = DateTime.Now + timeToLive
                });
        }

        public async Task Remove(string key, CancellationToken ct) {
            using (await synclock.WriterLockAsync(ct)) {
                this.cache.Remove(key);
            }
        }

        private bool TryGetValue(string key, out LazyLock<T> value) {
            value = (LazyLock<T>)this.cache.Get(key);
            if (value != null) {
                return true;
            }
            return false;
        }

        private sealed class LazyLock<L> {
            private volatile bool got;
            private L value;
            private readonly AsyncLock mutex;

            public LazyLock() {
                mutex = new AsyncLock();
            }

            public L Get(Func<L> activator) {
                if (!got) {
                    if (activator == null) {
                        return default(L);
                    }

                    using (mutex.Lock(CancellationToken.None)) {
                        if (!got) {
                            value = activator();

                            got = true;
                        }
                    }
                }

                return value;
            }

            public async Task<L> GetAsync(Func<Task<L>> activator) {
                if (!got) {
                    if (activator == null) {
                        return default(L);
                    }

                    using (await mutex.LockAsync(CancellationToken.None)) {
                        if (!got) {
                            value = await activator();

                            got = true;
                        }
                    }
                }

                return value;
            }
        }
    }
}