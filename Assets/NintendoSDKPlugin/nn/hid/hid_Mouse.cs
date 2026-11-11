/*--------------------------------------------------------------------------------*
  Copyright (C)Nintendo All rights reserved.

  These coded instructions, statements, and computer programs contain proprietary
  information of Nintendo and/or its licensed developers and are protected by
  national and international copyright laws. They may not be disclosed to third
  parties or copied or duplicated in any form, in whole or in part, without the
  prior written consent of Nintendo.

  The content herein is highly confidential and should be handled accordingly.
 *--------------------------------------------------------------------------------*/

using System;
using System.Runtime.InteropServices;

namespace nn.hid
{
    public partial class Mouse
    {
        public const int StateCountMax = 16;
    }

    [Flags]
    public enum MouseButton : int
    {
        Left = 0x1 << 0,
        Right = 0x1 << 1,
        Middle = 0x1 << 2,
        Forward = 0x1 << 3,
        Back = 0x1 << 4,
    }

    [Flags]
    public enum MouseAttribute : int
    {
        None,
        Transferable = 0x1 << 0,
        IsConnected = 0x1 << 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct MouseState
    {
        public readonly long samplingNumber;
        public readonly int x;
        public readonly int y;
        public readonly int deltaX;
        public readonly int deltaY;
        public readonly int wheelDelta;
        public readonly int sideWheelDelta;
        public readonly MouseButton buttons;
        public readonly MouseAttribute attributes;

        public override string ToString()
        {
            return string.Format("Position({0},{1}) Delta({2},{3}) Wheel({4},{5}) [{6}] {7} {8}",
                x, y, deltaX, deltaY, wheelDelta, sideWheelDelta, buttons, attributes, samplingNumber);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseHandle
    {
        public uint _storage;
    }

    public partial class Mouse
    {
#if !UNITY_SWITCH || UNITY_EDITOR
        public static MouseHandle GetHandle()
        {
            return new MouseHandle();
        }

        public static MouseHandle GetDebugMouseHandle()
        {
            return new MouseHandle();
        }

        public static void Initialize(MouseHandle handle)
        {
        }

        public static void GetState(ref MouseState pOutValue, MouseHandle handle)
        {
        }

        public static int GetStates(MouseState[] outValues, MouseHandle handle)
        {
            return 0;
        }
#else
        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_GetMouseHandle")]
        public static extern MouseHandle GetHandle();

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_GetDebugMouseHandle")]
        public static extern MouseHandle GetDebugMouseHandle();

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_InitializeMouse")]
        public static extern void Initialize(MouseHandle handle);

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_GetMouseState")]
        public static extern void GetState(ref MouseState pOutValue, MouseHandle handle);

        public static int GetStates(MouseState[] outValues, MouseHandle handle)
        {
            return GetStates(outValues, outValues.Length, handle);
        }

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_GetMouseStates")]
        private static extern int GetStates([In] MouseState[] outValues, int count, MouseHandle handle);
#endif
    }
}
