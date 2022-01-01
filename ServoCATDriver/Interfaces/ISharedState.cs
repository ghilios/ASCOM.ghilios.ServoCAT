#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Ninject;
using System;

namespace ASCOM.ghilios.ServoCAT.Interfaces {

    public interface ISharedState {
        string TelescopeDriverId { get; }

        string TelescopeDriverDescription { get; }

        IKernel Kernel { get; }

        TimeSpan DeviceConnectionTimeout { get; }

        TimeSpan DeviceReadTimeout { get; }

        TimeSpan DeviceWriteTimeout { get; }

        bool StartedByCOM { get; }
    }
}