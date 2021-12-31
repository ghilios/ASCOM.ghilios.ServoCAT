#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Interfaces;
using System;
using System.Runtime.Caching;
using System.Threading;

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
        private ReaderWriterLockSlim synclock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        public bool Contains(string key) {
            synclock.EnterReadLock();
            try {
                return this.cache.Contains(key);
            } finally {
                synclock.ExitReadLock();
            }
        }

        public T GetOrAdd(string key, Func<T> loadFunction, Func<CacheItemPolicy> getCacheItemPolicyFunction) {
            LazyLock<T> lazy;
            bool success;

            synclock.EnterReadLock();
            try {
                success = this.TryGetValue(key, out lazy);
            } finally {
                synclock.ExitReadLock();
            }

            if (!success) {
                synclock.EnterWriteLock();
                try {
                    if (!this.TryGetValue(key, out lazy)) {
                        lazy = new LazyLock<T>();
                        var policy = getCacheItemPolicyFunction();
                        this.cache.Add(key, lazy, policy);
                    }
                } finally {
                    synclock.ExitWriteLock();
                }
            }

            return lazy.Get(loadFunction);
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

        public void Remove(string key) {
            synclock.EnterWriteLock();
            try {
                this.cache.Remove(key);
            } finally {
                synclock.ExitWriteLock();
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

            public L Get(Func<L> activator) {
                if (!got) {
                    if (activator == null) {
                        return default(L);
                    }

                    lock (this) {
                        if (!got) {
                            value = activator();

                            got = true;
                        }
                    }
                }

                return value;
            }
        }
    }
}