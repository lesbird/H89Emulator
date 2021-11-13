using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate void ExternalInputDidReceiveKey(KeyCode receivedKey);
public delegate void ExternalInputDidReceiveKeyDown(KeyCode keyCode);
public delegate void ExternalInputDidReceiveKeyUp(KeyCode keyCode);

public class ExternalInputController : MonoBehaviour {

	public static ExternalInputController instance = null;

	public event ExternalInputDidReceiveKey OnExternalInputDidReceiveKey;
	public event ExternalInputDidReceiveKeyDown OnExternalInputDidReceiveKeyDown;
	public event ExternalInputDidReceiveKeyUp OnExternalInputDidReceiveKeyUp;

	public int[] iOSKeyCodeToUnityKeyCode = {
		  0,  0,  0,277,
		 97, 98, 99,100,101,102,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122, // a-z
		 49, 50, 51, 52, 53, 54, 55, 56, 57, 48, // 1-9,0 (30-39)
		 13, 27,  8,  9, 32, 45, 61, 91, 93, 92, // (40-49)
		 35, 59, 39, 96, 44, 46, 47,301,282,283, // (50-59)
		284,285,286,287,288,289,290,291,292,293, // (60-69)
		316,302, 19,277,278,280,127,279,281,275, // (70-79)
		276,274,273,300,267,268,269,270,271,257, // (80-89)
		258,259,260,261,262,263,264,265,256,266, // (90-99)
		  0,  0,  0,  0,294,295,296,  0,  0,  0, // (100-109)
		  0,  0,  0,  0,  0,  0,  0,315,319,  0, // (110-119)
		  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, // (120-129)
		  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, // (130-139)
		  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, // (140-149)
		  0,  0,  0,  0,317,  0,  0,  0,  0,  0, // (150-159)
		  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, // (160-169)
		  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, // (170-179)
		  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, // (180-189)
		  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, // (190-199)
		  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, // (200-209)
		  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, // (210-219)
		  0,  0,  0,  0,306,304,308,310,305,303, // (220-229)
		307,309,  0,  0,  0,  0,  0,  0,  0,  0, // (230-239)
		  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, // (240-249)
	};

	void Awake()
	{
		if(instance == null)
		{
			instance = this;
		} else if(instance != this)
		{
			Destroy(gameObject);
		}
	}

	void Start()
	{
		ExternalInputInterface.SetupExternalInput();
	}

	public void DidReceiveKeystroke(string receivedKeystroke)
	{
		KeyCode receivedKeyCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), receivedKeystroke);

		if(OnExternalInputDidReceiveKey != null)
			OnExternalInputDidReceiveKey(receivedKeyCode);
	}

	public void DidReceiveKeyDown(string keyCodeStr)
	{
		//KeyCode receivedKeyCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), receivedKeystroke);
		int n = int.Parse(keyCodeStr);
		if (n >= 0 && n < iOSKeyCodeToUnityKeyCode.Length)
		{
			KeyCode keyCode = (KeyCode)iOSKeyCodeToUnityKeyCode[n];
			if (OnExternalInputDidReceiveKeyDown != null)
			{
				OnExternalInputDidReceiveKeyDown(keyCode);
			}
		}
	}

	public void DidReceiveKeyUp(string keyCodeStr)
	{
		//KeyCode receivedKeyCode = (KeyCode)System.Enum.Parse(typeof(KeyCode), receivedKeystroke);
		int n = int.Parse(keyCodeStr);
		if (n >= 0 && n < iOSKeyCodeToUnityKeyCode.Length)
		{
			KeyCode keyCode = (KeyCode)iOSKeyCodeToUnityKeyCode[n];
			if (OnExternalInputDidReceiveKeyUp != null)
			{
				OnExternalInputDidReceiveKeyUp(keyCode);
			}
		}
	}
}
