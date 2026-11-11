#if UNITY_SWITCH
using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

#if UNITY_6000_7_OR_NEWER
using Applet = UnityEngine.Nintendo.Applet;
#else
using Applet = UnityEngine.Switch.Applet;
#endif

namespace UnityEngine.InputSystem.Switch
{
#if UNITY_INPUTSYSTEM_SWITCH_CONTROLLERAPPLET
	public static class ControllerSupportApplet
	{
		//MUST be the same as UnityEngine.Switch.Input.ControllerSupportArg
		
		/// <summary>
		/// Argument for settings properties on the Controller Support Applet
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct ControllerSupportArg
		{
			const int k_MaxControllerCount = 8;

			/// <summary>
			/// Set the values to defaults
			///
			/// Player Count Min = 0
			/// Player Count Max = 4
			/// Take Over Connection = true
			/// Enable Left Justify = true
			/// Enable Permit Dual Joy = true
			/// Enable Single (Player) Mode = false
			/// Enable Identification Color = false
			/// Enable Explain Text = false
			/// Identification Colour Array[8] = Empty Array
			/// Explain Text Array[8] = Empty Array
			/// </summary>
			public void SetDefault()
			{
				playerCountMin = 0;
				playerCountMax = 4;
				takeOverConnection = true;
				enableLeftJustify = true;
				enablePermitJoyDual = true;
				enableSingleMode = false;
				enableIdentificationColor = false;
				enableExplainText = false;
				identificationColour = new Color32[k_MaxControllerCount];
				explainText = new string[k_MaxControllerCount];
			}

			public byte playerCountMin;
			public byte playerCountMax;
			public bool takeOverConnection;
			public bool enableLeftJustify;
			public bool enablePermitJoyDual;
			public bool enableSingleMode;
			public bool enableIdentificationColor;
			public bool enableExplainText;
			public Color32[] identificationColour;
			public string[] explainText;
		}

		/// <summary>
		/// Additional information from the controller applet
		///
		/// If <see cref="ControllerSupportArg.enableSingleMode"/> is set it will return the used NPad ID of the selected controller
		/// Alternatively, the number of connected controllers will be set
		/// </summary>
		public struct ResultInfo
		{
			internal ResultInfo(UnityEngine.Switch.Input.ResultInfo nativeResult)
			{
				PlayerCount = nativeResult.playerCount;
				NPadID = (NPad.NpadId)nativeResult.idType;
			}

			public int PlayerCount { get; private set; }
			public NPad.NpadId NPadID { get; private set; }
		}

		/// <summary>
		/// Result from the controller applet call
		/// </summary>
		public enum Result : byte
		{
			Success = 0x1,
			Cancelled = 0x2, //nn::hid::ResultControllerSupportCanceled
			NotSupportedNpadStyle = 0x3, //nn::hid::ResultControllerSupportNotSupportedNpadStyle
			Error = 0x4, //nn::hid::ResultControllerSupportError
			ErrorUnknown = 0x5, //Case not handled by other error codes
		}

		/// <summary>
		/// Open the controller applet with a given setup of parameters
		/// </summary>
		/// <param name="arg">Configuration of the controller applet</param>
		/// <param name="suspendUnityThreads">If Unity threads, such as the audio thread, should be suspended while the applet is open</param>
		/// <returns></returns>
		public static Result Open(ControllerSupportArg arg, bool suspendUnityThreads = true)
		{
			return Open(arg, out _, suspendUnityThreads);
		}

		/// <summary>
		/// Open the controller applet with a given setup of parameters
		/// </summary>
		/// <param name="arg">Configuration of the controller applet</param>
		/// <param name="resultInfo">Additional result from the applet, changes based on if <see cref="ControllerSupportArg.enableSingleMode"/> is set</param>
		/// <param name="suspendUnityThreads">If Unity threads, such as the audio thread, should be suspended while the applet is open</param>
		/// <returns></returns>
		public static Result Open(ControllerSupportArg arg, out ResultInfo resultInfo, bool suspendUnityThreads = true)
		{
			if(suspendUnityThreads)
				Applet.Begin();

			var nativeResult = new UnityEngine.Switch.Input.ResultInfo();
			
			//Fast copy as the memory layout between the class here and UnityEngine class are the same
			UnityEngine.Switch.Input.ControllerSupportArg controllerSupportArg = UnsafeUtility.As<ControllerSupportArg, UnityEngine.Switch.Input.ControllerSupportArg>(ref arg);

			var result = (Result)UnityEngine.Switch.Input.OpenControllerApplet(controllerSupportArg, ref nativeResult);
			resultInfo = new ResultInfo(nativeResult);

			if(suspendUnityThreads)
				Applet.End();

			return result;
		}
	}
#endif
}
#endif
