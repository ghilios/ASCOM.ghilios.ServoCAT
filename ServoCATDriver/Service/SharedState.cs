﻿#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Interfaces;
using Ninject;
using System;
using System.Runtime.InteropServices;

namespace ASCOM.ghilios.ServoCAT.Service {

    public class SharedState : ISharedState {

        public SharedState() {
            TelescopeDriverId = ((ProgIdAttribute)Attribute.GetCustomAttribute(typeof(Telescope.Telescope), typeof(ProgIdAttribute))).Value;
            TelescopeDriverDescription = ((ServedClassNameAttribute)Attribute.GetCustomAttribute(typeof(Telescope.Telescope), typeof(ServedClassNameAttribute))).DisplayName;
        }

        public string TelescopeDriverId { get; private set; }

        public string TelescopeDriverDescription { get; private set; }

        public IKernel Kernel => CompositionRoot.Kernel;

        public TimeSpan DeviceConnectionTimeout => TimeSpan.FromSeconds(5);

        public TimeSpan DeviceReadTimeout => TimeSpan.FromSeconds(2);

        public TimeSpan DeviceWriteTimeout => TimeSpan.FromSeconds(2);
    }
}