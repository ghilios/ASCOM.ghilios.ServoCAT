#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Astrometry;
using ASCOM.ghilios.ServoCAT.Utility;

namespace ASCOM.ghilios.ServoCAT.Telescope {

    public class ServoCatFirmwareAxisConfig : BaseINPC {
        private short encoderResolution;

        public short EncoderResolution {
            get => encoderResolution;
            set {
                encoderResolution = value;
                RaisePropertyChanged();
            }
        }

        private short gearRatioValue1;

        public short GearRatioValue1 {
            get => gearRatioValue1;
            set {
                gearRatioValue1 = value;
                RaisePropertyChanged();
            }
        }

        private short slewRateValue1_TDPS;

        public short SlewRateValue1_TDPS {
            get => slewRateValue1_TDPS;
            set {
                slewRateValue1_TDPS = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SlewRatePerSecond1));
            }
        }

        private short jogRateValue1_AMPS;

        public short JogRateValue1_AMPS {
            get => jogRateValue1_AMPS;
            set {
                jogRateValue1_AMPS = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(JogRatePerSecond1));
            }
        }

        private short guideRateValue1_ASPS;

        public short GuideRateValue1_ASPS {
            get => guideRateValue1_ASPS;
            set {
                guideRateValue1_ASPS = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(GuideRatePerSecond1));
            }
        }

        public Angle SlewRatePerSecond1 {
            get => Angle.ByDegree(SlewRateValue1_TDPS / 10.0d);
        }

        public Angle JogRatePerSecond1 {
            get => Angle.ByDegree(JogRateValue1_AMPS / 60.0d);
        }

        public Angle GuideRatePerSecond1 {
            get => Angle.ByDegree(GuideRateValue1_ASPS / 3600.0d);
        }

        private short slewRateValue2_TDPS;

        public short SlewRateValue2_TDPS {
            get => slewRateValue2_TDPS;
            set {
                slewRateValue2_TDPS = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SlewRatePerSecond2));
            }
        }

        private short jogRateValue2_AMPS;

        public short JogRateValue2_AMPS {
            get => jogRateValue2_AMPS;
            set {
                jogRateValue2_AMPS = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(JogRatePerSecond2));
            }
        }

        private short guideRateValue2_ASPS;

        public short GuideRateValue2_ASPS {
            get => guideRateValue2_ASPS;
            set {
                guideRateValue2_ASPS = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(GuideRatePerSecond2));
            }
        }

        public Angle SlewRatePerSecond2 {
            get => Angle.ByDegree(SlewRateValue2_TDPS / 10.0d);
        }

        public Angle JogRatePerSecond2 {
            get => Angle.ByDegree(JogRateValue2_AMPS / 60.0d);
        }

        public Angle GuideRatePerSecond2 {
            get => Angle.ByDegree(GuideRateValue2_ASPS / 3600.0d);
        }

        private short accelDecelRateSecs;

        public short AccelDecelRateSecs {
            get => accelDecelRateSecs;
            set {
                accelDecelRateSecs = value;
                RaisePropertyChanged();
            }
        }

        private short backlashValue;

        public short BacklashValue {
            get => backlashValue;
            set {
                backlashValue = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(BacklashArcSeconds));
            }
        }

        public int BacklashArcSeconds {
            get {
                // >= 1000 represents arcsecs, otherwise arcmins
                if (BacklashValue >= 1000) {
                    return BacklashValue - 1000;
                }
                return BacklashValue * 60;
            }
        }

        private short axisLimit;

        public short AxisLimit {
            get => axisLimit;
            set {
                axisLimit = value;
                RaisePropertyChanged();
            }
        }

        private bool trackDirectionPositive;

        public bool TrackDirectionPositive {
            get => trackDirectionPositive;
            set {
                trackDirectionPositive = value;
                RaisePropertyChanged();
            }
        }

        private bool goToDirectionPositive;

        public bool GoToDirectionPositive {
            get => goToDirectionPositive;
            set {
                goToDirectionPositive = value;
                RaisePropertyChanged();
            }
        }

        public void CopyFrom(ServoCatFirmwareAxisConfig other) {
            this.EncoderResolution = other.EncoderResolution;
            this.GearRatioValue1 = other.GearRatioValue1;
            this.SlewRateValue1_TDPS = other.SlewRateValue1_TDPS;
            this.JogRateValue1_AMPS = other.JogRateValue1_AMPS;
            this.GuideRateValue1_ASPS = other.GuideRateValue1_ASPS;
            this.SlewRateValue2_TDPS = other.SlewRateValue2_TDPS;
            this.JogRateValue2_AMPS = other.JogRateValue2_AMPS;
            this.GuideRateValue2_ASPS = other.GuideRateValue2_ASPS;
            this.AccelDecelRateSecs = other.AccelDecelRateSecs;
            this.BacklashValue = other.BacklashValue;
            this.AxisLimit = other.AxisLimit;
            this.TrackDirectionPositive = other.TrackDirectionPositive;
            this.GoToDirectionPositive = other.GoToDirectionPositive;
        }
    }

    public class ServoCatFirmwareConfig : BaseINPC {

        public ServoCatFirmwareConfig() : this(new ServoCatFirmwareAxisConfig(), new ServoCatFirmwareAxisConfig()) {
        }

        public ServoCatFirmwareConfig(ServoCatFirmwareAxisConfig azimuthConfig, ServoCatFirmwareAxisConfig altitudeConfig) {
            this.AzimuthConfig = azimuthConfig;
            this.AltitudeConfig = altitudeConfig;
        }

        public ServoCatFirmwareAxisConfig AzimuthConfig { get; private set; }
        public ServoCatFirmwareAxisConfig AltitudeConfig { get; private set; }
        private short easyTrackLatitudeValue;

        public short EasyTrackLatitudeValue {
            get => easyTrackLatitudeValue;
            set {
                easyTrackLatitudeValue = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(EasyTrackLatitude));
            }
        }

        public Angle EasyTrackLatitude {
            get => Angle.ByDegree(EasyTrackLatitudeValue / 500.0d); // 500 * latitude
        }

        private short easyTrackSignValue;

        public short EasyTrackSignValue {
            get => easyTrackSignValue;
            set {
                easyTrackSignValue = value;
                RaisePropertyChanged();
            }
        }

        public short EasyTrackSign {
            // Valid values are 0-3. The config stores this as * 500 (0, 500, 1000, 1500)
            get => (short)(EasyTrackSignValue / 500);
        }

        public void CopyFrom(ServoCatFirmwareConfig other) {
            AzimuthConfig.CopyFrom(other.AzimuthConfig);
            AltitudeConfig.CopyFrom(other.AltitudeConfig);
            EasyTrackLatitudeValue = other.EasyTrackLatitudeValue;
            EasyTrackSignValue = other.EasyTrackSignValue;
        }
    }
}