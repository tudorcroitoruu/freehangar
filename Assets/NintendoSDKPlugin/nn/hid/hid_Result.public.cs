/*--------------------------------------------------------------------------------*
  Copyright (C)Nintendo All rights reserved.

  These coded instructions, statements, and computer programs contain proprietary
  information of Nintendo and/or its licensed developers and are protected by
  national and international copyright laws. They may not be disclosed to third
  parties or copied or duplicated in any form, in whole or in part, without the
  prior written consent of Nintendo.

  The content herein is highly confidential and should be handled accordingly.
 *--------------------------------------------------------------------------------*/

#if UNITY_SWITCH || UNITY_EDITOR || NN_PLUGIN_ENABLE 
namespace nn.hid
{
    public static partial class VibrationFile
    {
        public static ErrorRange ResultInvalid { get { return new ErrorRange(202, 140, 150); } }
    }
    public static partial class Npad
    {
        public static ErrorRange ResultControllerNotConnected { get { return new ErrorRange(202, 604, 605); } }
    }
    public static partial class SensorFusion
    {
        public static ErrorRange ResultNotEnabled => new ErrorRange(202, 5261, 5262);
        public static ErrorRange ResultFirmwareUpdateRequired => new ErrorRange(202, 5262, 5263);
        public static ErrorRange ResultDeviceNotConnected => new ErrorRange(202, 5263, 5264);
    }
}
#endif
