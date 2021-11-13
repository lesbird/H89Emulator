using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExternalInputInterface {

	[DllImport("__Internal")]
	private static extern void _setupExternalInput ();
	public static void SetupExternalInput()
	{
		if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.tvOS)
			_setupExternalInput ();
	}
}
