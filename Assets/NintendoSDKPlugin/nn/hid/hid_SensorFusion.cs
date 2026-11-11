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
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace nn.hid
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SensorFusionHandle
    {
        public int _storage;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct AccelerationType
    {
        public nn.util.Float3 _storage;

#if !UNITY_SWITCH || UNITY_EDITOR
        public nn.util.Float3 GetG()
        {
            return new nn.util.Float3();
        }

        public nn.util.Float3 GetMetersPerSecondSquared()
        {
            return new nn.util.Float3();
        }
#else
        public nn.util.Float3 GetG()
        {
            var ret = new nn.util.Float3();
            AccelerationType.GetG(ref this, ref ret.x, ref ret.y, ref ret.z);
            return ret;
        }

        public nn.util.Float3 GetMetersPerSecondSquared()
        {
            var ret = new nn.util.Float3();
            AccelerationType.GetMetersPerSecondSquared(ref this, ref ret.x, ref ret.y, ref ret.z);
            return ret;
        }

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_GetAccelerationTypeG")]
        private static extern void GetG(
            ref AccelerationType inst, ref float pOutX, ref float pOutY, ref float pOutZ);

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_GetAccelerationTypeMetersPerSecondSquared")]
        private static extern void GetMetersPerSecondSquared(
            ref AccelerationType inst, ref float pOutX, ref float pOutY, ref float pOutZ);
#endif
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct AngularVelocityType
    {
        public nn.util.Float3 _storage;

#if !UNITY_SWITCH || UNITY_EDITOR
        public nn.util.Float3 GetDps()
        {
            return new nn.util.Float3();
        }

        public nn.util.Float3 GetRadiansPerSecond()
        {
            return new nn.util.Float3();
        }
#else
        public nn.util.Float3 GetDps()
        {
            var ret = new nn.util.Float3();
            AngularVelocityType.GetDps(ref this, ref ret.x, ref ret.y, ref ret.z);
            return ret;
        }

        public nn.util.Float3 GetRadiansPerSecond()
        {
            var ret = new nn.util.Float3();
            AngularVelocityType.GetRadiansPerSecond(ref this, ref ret.x, ref ret.y, ref ret.z);
            return ret;
        }

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_GetAngularVelocityTypeDps")]
        private static extern void GetDps(
            ref AngularVelocityType inst, ref float pOutX, ref float pOutY, ref float pOutZ);

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_GetAngularVelocityTypeRadiansPerSecond")]
        private static extern void GetRadiansPerSecond(
            ref AngularVelocityType inst, ref float pOutX, ref float pOutY, ref float pOutZ);
#endif
        };

    [StructLayout(LayoutKind.Sequential)]
    public struct RotationMatrixType
    {
        public nn.util.Float3 x;
        public nn.util.Float3 y;
        public nn.util.Float3 z;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SensorFusionAttributeSet
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        private uint[] _storage;

        public bool IsStill
        {
            get => (_storage[0] & 0x1) != 0;
        }

        public bool IsCalibrated
        {
            get => (_storage[0] & 0x2) != 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SensorFusionOrientation
    {
        public long samplingNumber;
        public long sequenceNumber;
        public long deltaTimeNanoSeconds;
        public AccelerationType acceleration;
        public AngularVelocityType angularVelocity;
        public SensorFusionAttributeSet attributes;

        public nn.util.Float4 quaternion;
        public RotationMatrixType rotationMatrix;

        public nn.util.Float4 GetQuaternion()
        {
            return quaternion;
        }

        public RotationMatrixType GetRotationMatrix()
        {
            return rotationMatrix;
        }
    };

    public static partial class SensorFusion
    {
        public const int StateCountMax = 16;
        public const int HandleCountMax = 2;

#if !UNITY_SWITCH || UNITY_EDITOR
        public static void Initialize()
        {
        }

        public static void Deinitialize()
        {
        }

        public static bool IsInitialized()
        {
            return false;
        }

        public static int GetHandles([Out] SensorFusionHandle[] outValues, int count, NpadId npadId, NpadStyle npadStyle)
        {
            return 0;
        }

        public static void Enable(SensorFusionHandle handle)
        {
        }

        public static void Disable(SensorFusionHandle handle)
        {
        }

        public static bool IsEnabled(SensorFusionHandle handle)
        {
            return false;
        }

        public static Result Update(SensorFusionHandle handle)
        {
            return new Result();
        }

        public static int GetOrientations([Out] SensorFusionOrientation[] outStates, int count, SensorFusionHandle handle)
        {
            return 0;
        }

        public static void ResetYaw(SensorFusionHandle handle)
        {
        }

        public static bool IsStill(SensorFusionHandle handle)
        {
            return false;
        }

        public static bool IsCalibrated(SensorFusionHandle handle)
        {
            return false;
        }
#else
        private static IntPtr _memory = IntPtr.Zero;
        private static UIntPtr _memorySize = (UIntPtr)0;

        private class WorkBuffer
        {
            public IntPtr ptr;
            public UIntPtr size;
        }

        private static Dictionary<SensorFusionHandle, WorkBuffer> _workBuffers = new Dictionary<SensorFusionHandle, WorkBuffer>();

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_AllocateMemoryAndInitializeSensorFusion")]
        private static extern void AllocateMemoryAndInitialize(ref IntPtr memory, ref UIntPtr memorySize);

        public static void Initialize()
        {
            if (_memory == IntPtr.Zero)
            {
                AllocateMemoryAndInitialize(ref _memory, ref _memorySize);
            }
        }

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_FinalizeSensorFusionAndReleaseMemory")]
        private static extern void FinalizeAndReleaseMemory(IntPtr memory);

        public static void Deinitialize()
        {
            foreach (var handle in _workBuffers.Keys)
            {
                DisableAndReleaseWorkBuffer(handle, _workBuffers[handle].ptr);
            }
            _workBuffers.Clear();

            if (_memory != IntPtr.Zero)
            {
                FinalizeAndReleaseMemory(_memory);
                _memory = IntPtr.Zero;
                _memorySize = (UIntPtr)0;
            }
        }

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_IsSensorFusionInitialized")]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool IsInitialized();

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_GetSensorFusionHandles")]
        public static extern int GetHandles([Out] SensorFusionHandle[] outValues, int count, NpadId npadId, NpadStyle npadStyle);

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_AllocateWorkBufferAndEnableSensorFusion")]
        private static extern void AllocateWorkBufferAndEnable(SensorFusionHandle handle, ref IntPtr workBuffer, ref UIntPtr workBufferSize);

        public static void Enable(SensorFusionHandle handle)
        {
            if (!_workBuffers.ContainsKey(handle))
            {
                var wb = new WorkBuffer();
                _workBuffers.Add(handle, wb);
                AllocateWorkBufferAndEnable(handle, ref wb.ptr, ref wb.size);
            }
        }

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_DisableSensorFusionAndReleaseWorkBuffer")]
        private static extern void DisableAndReleaseWorkBuffer(SensorFusionHandle handle, IntPtr workBuffer);

        public static void Disable(SensorFusionHandle handle)
        {
            if (_workBuffers.ContainsKey(handle))
            {
                DisableAndReleaseWorkBuffer(handle, _workBuffers[handle].ptr);
                _workBuffers.Remove(handle);
            }
        }

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_IsSensorFusionEnabled")]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool IsEnabled(SensorFusionHandle handle);

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_UpdateSensorFusion")]
        public static extern Result Update(SensorFusionHandle handle);

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_GetSensorFusionOrientations")]
        public static extern int GetOrientations([Out] SensorFusionOrientation[] outStates, int count, SensorFusionHandle handle);

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_ResetSensorFusionYaw")]
        public static extern void ResetYaw(SensorFusionHandle handle);

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_IsSensorFusionStill")]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool IsStill(SensorFusionHandle handle);

        [DllImport(Nn.DllName,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "nn_hid_IsSensorFusionCalibrated")]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool IsCalibrated(SensorFusionHandle handle);
#endif
    }
}
#endif
