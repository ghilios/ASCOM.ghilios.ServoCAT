#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Astrometry.AstroUtils;
using ASCOM.Astrometry.NOVAS;
using ASCOM.Astrometry.SOFA;
using ASCOM.ghilios.ServoCAT.Astrometry;
using ASCOM.ghilios.ServoCAT.Interfaces;
using ASCOM.ghilios.ServoCAT.IO;
using ASCOM.ghilios.ServoCAT.Telescope;
using ASCOM.ghilios.ServoCAT.Utility;
using ASCOM.ghilios.ServoCAT.ViewModel;
using ASCOM.Utilities;
using ASCOM.Utilities.Interfaces;
using Ninject;
using Ninject.Activation;
using Ninject.Modules;
using System;
using System.Runtime.InteropServices;

namespace ASCOM.ghilios.ServoCAT.Service {

    public static class CompositionRoot {

        static CompositionRoot() {
            Kernel = new StandardKernel(new CoreModule());
        }

        public static IKernel Kernel { get; }
    }

    internal class CoreModule : NinjectModule {

        public override void Load() {
            Bind<IMainVM>().To<MainVM>().InSingletonScope();
            Bind<TraceLogger>().ToConstructor(ctorArg => new TraceLogger("", "ghilios.ServoCAT.Server")).InSingletonScope().Named("Server");
            Bind<TraceLogger>().ToConstructor(ctorArg => new TraceLogger("", "ghilios.ServoCAT.Telescope")).InSingletonScope().Named("Telescope");
            Bind<TraceLogger>().ToConstructor(ctorArg => new TraceLogger("", "ghilios.ServoCAT.Serial")).InSingletonScope().Named("Serial");
            Bind<IAstroUtils>().To<AstroUtils>().InSingletonScope();
            Bind<ISOFA>().To<SOFA>().InSingletonScope();
            Bind<INOVAS31>().To<NOVAS31>().InSingletonScope();
            Bind<AstrometryConverter>().To<AstrometryConverter>().InSingletonScope();
            Bind<Util>().To<Util>().InSingletonScope();
            Bind<ISerialUtilities>().To<SerialUtilities>().InSingletonScope();
            Bind<IDriverConnectionManager>().To<DriverConnectionManager>().InSingletonScope();
            Bind<IServoCatOptions>().To<ServoCatOptions>().InSingletonScope().OnActivation(x => x.Load());
            Bind<IChannelFactory>().To<ChannelFactory>().InSingletonScope();
            Bind<IServoCatDevice>().To<ServoCatDevice>().InSingletonScope();
            Bind<ISharedState>().To<SharedState>().InSingletonScope();
            Bind<IMicroCacheFactory>().To<MicroCacheFactory>().InSingletonScope();
            Bind<IProfile>().ToMethod(CreateTelescopeProfile).InSingletonScope().Named("Telescope");
        }

        private static IProfile CreateTelescopeProfile(IContext context) {
            var profile = new Profile() {
                DeviceType = nameof(Telescope.Telescope)
            };

            var driverId = ((ProgIdAttribute)Attribute.GetCustomAttribute(typeof(Telescope.Telescope), typeof(ProgIdAttribute))).Value;
            if (!profile.IsRegistered(driverId)) {
                var assemblyTitleAttribute = Attribute.GetCustomAttribute(typeof(Telescope.Telescope), typeof(ServedClassNameAttribute));
                string chooserName = ((ServedClassNameAttribute)assemblyTitleAttribute).DisplayName;
                profile.Register(driverId, chooserName);
            }
            return profile;
        }
    }
}