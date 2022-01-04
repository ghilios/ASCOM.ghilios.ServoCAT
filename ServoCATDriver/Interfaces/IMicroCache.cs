#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.ghilios.ServoCAT.Interfaces {

    public interface IMicroCache<T> {

        Task<bool> Contains(string key, CancellationToken ct);

        T GetOrAdd(string key, Func<T> loadFunction, Func<CacheItemPolicy> getCacheItemPolicyFunction);

        T GetOrAdd(string key, Func<T> loadFunction, TimeSpan timeToLive);

        Task<T> GetOrAddAsync(string key, Func<Task<T>> loadFunction, Func<CacheItemPolicy> getCacheItemPolicyFunction, CancellationToken ct);

        Task<T> GetOrAddAsync(string key, Func<Task<T>> loadFunction, TimeSpan timeToLive, CancellationToken ct);

        Task Remove(string key, CancellationToken ct);
    }

    public interface IMicroCacheFactory {

        IMicroCache<T> Create<T>();

        IMicroCache<T> Create<T>(ObjectCache objectCache);
    }
}