using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Z80;

public class H89IO : IIO
{
	public bool intEnable;
	public bool romDisable;
	public bool sideSelect;

	public byte[] genericPort = new byte[256];

	// 0xF2
	// 0010.0000 = rom disable
	// 0100.0000 = side select (1 = side 1)
	// read returns status bit settings
	// 0010.0000 = default settings

	/// <summary>
	/// Reads a single byte from a port
	/// </summary>
	/// <param name="Port">Port to read from</param>
	/// <returns>The byte value</returns>
	public byte ReadPort(ushort Port)
	{
		int port = Port & 0x00FF;

		byte b = genericPort[port];

		// reads from 350q - 357q should be redirected to Z19IO
		if (port >= 0xE8 && port <= 0xEF)
		{
			// 0xE8 (+0) = data
			// 0xE9 (+1) = interrupt enable
			// 0xEA (+2) = interrupt id
			// 0xEB (+3) = line control
			// 0xEC (+4) = modem control
			// 0xED (+5) = line status
			// 0xEE (+6) = modem status
			// 0xEF (+7) = scratch
			if (port == 0xE8 && (genericPort[0xEB] & 0x80) == 0)
			{
				b = H89.Instance.h19Console.GetCharDirect();
				genericPort[0xE8] = b;
				genericPort[0xED] &= 0xFE;
			}
			if (port == 0xEC)
			{
				b = 0x03; // DTR/RTS
			}
			if (port == 0xED)
			{
				byte d = 0x60;
				if (H89.Instance.h19Console.HasCharDirect())
				{
					d |= 0x01;
				}
				genericPort[0xED] = d;
				b = d;
			}
			if (port == 0xEE)
			{
				b = 0x30; // DSR/CTS
			}
		}
		
		// reads from 174q - 177q should be redirected to H17
		if (port >= 0x7C && port <= 0x7F)
		{
			if (H17.Instance != null)
			{
				//Debug.Log("ReadPort() Port=" + port.ToString("X"));
				b = H17.Instance.H17IOReadByte(port);
			}
		}

		if (port == 0xF0)
		{
			b = 0;
		}

		if (port == 0xF2)
		{
			//Debug.Log("ReadPort() Port=" + port.ToString("X"));

			b = 0x20;
			if (sideSelect)
			{
				b |= 0x40;
			}
		}

		if (H89.Instance.logIOR)
		{
			Debug.Log("ReadPort() Port=" + port.ToString("X3") + " Value=" + b.ToString("X2"));
		}

		return b;
	}



	/// <summary>
	/// Writes a single byte to a port
	/// </summary>
	/// <param name="Port">Port to write to</param>
	/// <param name="Value">The byte value</param>
	public void WritePort(ushort Port, byte Value)
	{
		int port = Port & 0x00FF;

		if (H89.Instance.logIOW)
		{
			Debug.Log("WritePort() Port=" + port.ToString("X3") + " Value=" + Value.ToString("X2"));
		}

		// writes to 350q - 357q should be redirected to H19 terminal
		if (port >= 0xE8 && port <= 0xEF)
		{
			if (port == 0xE8 && (genericPort[0xEB] & 0x80) == 0)
			{
				H89.Instance.h19Console.InCharDirect(Value);
			}
			genericPort[port] = Value;
			return;
		}
		
		// writes to 174q - 177q should be redirected to H17
		if (port >= 0x7C && port <= 0x7F)
		{
			if (H17.Instance != null)
			{
				//Debug.Log("WritePort() Port=" + port.ToString("X") + " Value=" + Value.ToString("X"));
				H17.Instance.H17IOWriteByte(port, Value);
			}
			return;
		}

		if (port == 0xF0)
		{
			return;
		}
		/*
		if (Z89.Instance.logZ89WritePort)
		{
			Debug.Log("Z89.WritePort() Port=" + Z89.EncodeOctalString(port) + " Value=" + Value.ToString());
		}
		*/
		if (port == 0xF2)
		{
			//Debug.Log("WritePort() Port=" + port.ToString("X3") + " Value=" + Value.ToString("X2"));

			if ((Value & 0x02) != 0)
			{
				intEnable = true;
			}
			else
			{
				intEnable = false;
			}

			if ((Value & 0x20) != 0)
			{
				romDisable = true;
			}
			else
			{
				romDisable = false;
			}

			if ((Value & 0x40) != 0)
			{
				//Debug.Log("sideSelect ON");
				sideSelect = true;
			}
			else
			{
				sideSelect = false;
			}
			genericPort[0xF2] = Value;
			return;
		}

		if (port == 0xFA || port == 0xFB)
		{
			return;
		}

		Debug.Log("WritePort() Port=" + port.ToString("X3") + " Value=" + Value.ToString("X2"));

		genericPort[port] = Value;
	}
}
