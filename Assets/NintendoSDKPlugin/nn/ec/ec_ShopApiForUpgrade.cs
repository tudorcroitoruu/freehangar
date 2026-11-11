/*--------------------------------------------------------------------------------*
  Copyright (C)Nintendo All rights reserved.

  These coded instructions, statements, and computer programs contain proprietary
  information of Nintendo and/or its licensed developers and are protected by
  national and international copyright laws. They may not be disclosed to third
  parties or copied or duplicated in any form, in whole or in part, without the
  prior written consent of Nintendo.

  The content herein is highly confidential and should be handled accordingly.
 *--------------------------------------------------------------------------------*/

#if (UNITY_SWITCH || UNITY_EDITOR || NN_PLUGIN_ENABLE) && NN_EC_SHOP_SERVICE_ENABLE
using System.Runtime.InteropServices;

namespace nn.ec
{
    public static partial class Shop
    {
#if !UNITY_SWITCH || UNITY_EDITOR
        public static void ShowApplicationInformationForUpgrade(nn.ec.UpgradeEdition edition) { }
        public static void ShowApplicationInformationForUpgrade(nn.ec.UpgradeEdition edition, nn.account.UserHandle selectedUser) { }
        public static void ShowApplicationInformationForUpgrade(nn.ec.UpgradeEdition edition, ulong applicationId) { }
        public static void ShowApplicationInformationForUpgrade(nn.ec.UpgradeEdition edition, ulong applicationId, nn.account.UserHandle selectedUser) { }
#else
        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_ec_ShowShopApplicationInformationForUpgradeSelf1")]
        public static extern void ShowApplicationInformationForUpgrade(nn.ec.UpgradeEdition edition);

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_ec_ShowShopApplicationInformationForUpgradeSelf2")]
        public static extern void ShowApplicationInformationForUpgrade(
            nn.ec.UpgradeEdition edition, nn.account.UserHandle selectedUser);

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_ec_ShowShopApplicationInformationForUpgrade1")]
        public static extern void ShowApplicationInformationForUpgrade(
            ulong applicationId, nn.ec.UpgradeEdition edition);

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_ec_ShowShopApplicationInformationForUpgrade2")]
        public static extern void ShowApplicationInformationForUpgrade(
            ulong applicationId, nn.ec.UpgradeEdition edition, nn.account.UserHandle selectedUser);
#endif
    }
}
#endif