using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Z80;

/// <summary>
/// IMemory implementation for a 64k H89
/// </summary>
public class H89Memory : IMemory
{
	byte[] _Memory = new byte[0x10000];

	/// <summary>
	/// Raised before read
	/// </summary>
	public event OnReadHandler OnRead;

	/// <summary>
	/// Raised before write
	/// </summary>
	public event OnWriteHandler OnWrite;

	/// <summary>
	/// Initialize memory and load rom
	/// </summary>
	public H89Memory()
	{
		LoadROM();
		LoadH17ROM();
	}

	/// <summary>
	/// Load a ROM file from address 0x0000 to address 0x3FFF
	/// </summary>
	public void LoadROM()
	{
		string diskpath = Application.streamingAssetsPath + "/ROMS/2732_444-142_MTR90A.ROM";
		if (File.Exists(diskpath))
		{
			byte[] data = File.ReadAllBytes(diskpath);
			if (data.Length > 0)
			{
				Debug.Log("LoadROM() done - size=" + data.Length.ToString());
				data.CopyTo(_Memory, 0);
			}
		}
	}

	/// <summary>
	/// Load a ROM file from address 0x0000 to address 0x3FFF
	/// </summary>
	public void LoadH17ROM()
	{
		string diskpath = Application.streamingAssetsPath + "/ROMS/2716_444-19_H17.ROM";
		if (File.Exists(diskpath))
		{
			byte[] data = File.ReadAllBytes(diskpath);
			if (data.Length > 0)
			{
				Debug.Log("LoadH17ROM() done - size=" + data.Length.ToString());
				data.CopyTo(_Memory, 0x1800);
			}
		}
	}

	/// <summary>
	/// Reads a memory cell
	/// </summary>
	/// <param name="Address">Address to read</param>
	/// <returns>The byte readed</returns>
	public byte ReadByte(ushort Address)
	{
		if (OnRead != null)
		{
			OnRead(Address);
		}
		return _Memory[Address];
	}

	/// <summary>
	/// Writes a memory cell
	/// </summary>
	/// <param name="Address">Address to write</param>
	/// <param name="Value">The byte to write</param>
	public void WriteByte(ushort Address, byte Value)
	{
		if (Address < 0x2000)
		{
			if (!H89.Instance.h89IO.romDisable && !H17.Instance.writeEnable)
			{
				return;
			}
		}

		_Memory[Address] = Value;

		if (OnWrite != null)
		{
			OnWrite(Address, Value);
		}
	}

	/// <summary>
	/// Read a word from memory
	/// </summary>
	/// <param name="Address">Address to read</param>
	/// <returns>the word readed</returns>
	public ushort ReadWord(ushort Address)
	{
		return (ushort)(ReadByte((ushort)(Address + 1)) << 8 | ReadByte(Address));
	}

	/// <summary>
	/// Write a word to memory
	/// </summary>
	/// <param name="Address">Address</param>
	/// <param name="Value">The word to write</param>
	public void WriteWord(ushort Address, ushort Value)
	{
		WriteByte(Address, (byte)(Value & 0x00FF));
		WriteByte((ushort)(Address + 1), (byte)((Value & 0xFF00) >> 8));
	}

	/// <summary>
	/// Memory size
	/// </summary>
	public int Size
	{
		get
		{
			return 0x10000;
		}
	}

	/// <summary>
	/// Access to whole memory
	/// </summary>
	public byte this [int Address]
	{
		get
		{
			return _Memory[Address];
		}
		set
		{
			_Memory[Address] = value;
		}
	}

	/// <summary>
	/// Memory as byte array
	/// </summary>
	public byte[] Raw
	{
		get
		{
			return _Memory;
		}
	}
}
