using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class H17 : MonoBehaviour
{
    [HideInInspector]
    public ulong statesPerHole;
    [HideInInspector]
	public ulong statesOverHole;
    public UnityEngine.UI.Text diskHoleText;
    public UnityEngine.UI.Text sectorBufferIndexText;
    public UnityEngine.UI.Text diskTrackText;

    public bool motorOn;
    public bool writeEnable;
    public bool writeGateEnable;
    public bool syncDetect;
    public bool writeProtect;
    public bool dataAvail;

    private int stepDirection;
    private bool diskHole;
    private int diskSector;
    private int[] diskVol = new int[3];
    private int driveSelect;
    private byte[] portAdr = new byte[256];

    private string[] diskFileName = new string[3];
    private byte[] disk0Buffer = new byte[1024000]; // 1mb (100K, 200K, 400K, 800K)
    private byte[] disk1Buffer = new byte[1024000]; // 1mb (100K, 200K, 400K, 800K)
    private byte[] disk2Buffer = new byte[1024000]; // 1mb (100K, 200K, 400K, 800K)
    private byte[] diskBuffer;
	private byte[] trackWriteBuffer = new byte[4096];
    private byte[] trackSectorMarker = new byte[4096];
    private byte[] sectorBuffer = new byte[5 + 1 + 256 + 1];
    private int sectorBufferIndex;
    private int sectorBufferSize;
    private int writeBufferIndex;
    private int syncCharCount;
    private int[] diskTrack = new int[3];
    private int[] diskSides = new int[3];
    private int[] diskNumTracks = new int[3];
    private bool[] isHDOS = new bool[3];
    private int checksum;
    private bool canWrite;

	[HideInInspector]
	public int writeBufferCount;
	[HideInInspector]
	public int writeCount;

	private ulong lastHoleTime;
	private ulong lastHoleTicks;
    private int holeCount;

    public UnityEngine.UI.Text timingText;
    private long lastms;
    private long lastmselapsed;
    private long lastts;
    private long lasttselapsed;

    public struct DiskImage
    {
        public string filePath;
        public string displayName;
    }
    private List<DiskImage> diskImagesList = new List<DiskImage>();
    private List<string> diskNameList;

    public UnityEngine.UI.Dropdown sy0Dropdown;
    public UnityEngine.UI.Dropdown sy1Dropdown;
    public UnityEngine.UI.Dropdown sy2Dropdown;

    public static H17 Instance;

    // Sector Header
    // 0 = 0xFD
    // 1 = vol num
    // 2 = track num
    // 3 = sector num
    // 4 = checksum
    // Sector Data
    // 0 = 0xFD
    // 1 - 256 data
    // 257 = checksum

    // 0x7C = data port (174Q)
    // 0x7D = status port (175Q)
    // 0x7E = sync char (176Q)
    // 0x7F = disk control (177Q)
    //

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        diskBuffer = disk0Buffer;
        BuildDiskImageLibrary();
    }

    void Update()
    {
        statesPerHole = (ulong)H89.Instance.statesPerInt1 * 10;
        statesOverHole = (ulong)H89.Instance.statesPerInt1;

        int side = (H89.Instance.h89IO.sideSelect) ? 1 : 0;
        diskTrackText.text = "DRV:" + driveSelect.ToString() + " TRK:" + diskTrack[driveSelect].ToString() + " SIDE:" + side.ToString();
        timingText.text = "H17 tselapsed=" + lasttselapsed.ToString() + " mselasped=" + lastmselapsed.ToString();
        sectorBufferIndexText.text = "SECIDX=" + sectorBufferIndex.ToString();

		H89Timing.Instance.motorOn = motorOn;
		H89Timing.Instance.diskTrack[0] = diskTrack[0];
		H89Timing.Instance.diskTrack[1] = diskTrack[1];
		H89Timing.Instance.diskTrack[2] = diskTrack[2];
		H89Timing.Instance.diskSector = diskSector;
		H89Timing.Instance.holeCount = holeCount;
    }

    void BuildDiskImageLibrary()
    {
        string path = Application.streamingAssetsPath;
        path = System.IO.Path.Combine(path, "DiskImages");
        string[] files = System.IO.Directory.GetFiles(path, "*.?8?", System.IO.SearchOption.AllDirectories);
        string key = "DiskImages" + System.IO.Path.DirectorySeparatorChar;

        List<string> diskPathList = new List<string>(files);
        diskPathList.Sort();

        diskNameList = new List<string>();
        for (int i = 0; i < diskPathList.Count; i++)
        {
            int idx = diskPathList[i].IndexOf(key) + key.Length;
            string displayName = diskPathList[i].Substring(idx);
            diskNameList.Add(displayName);

            DiskImage diskImage = new DiskImage();
            diskImage.filePath = diskPathList[i];
            diskImage.displayName = displayName;
            diskImagesList.Add(diskImage);
        }
        diskNameList.Insert(0, "NONE");

        DiskImage noneImage = new DiskImage();
        noneImage.displayName = "NONE";
        noneImage.filePath = string.Empty;
        diskImagesList.Insert(0, noneImage);

		// get all user disk images and append to diskImageList
		string userPath = Application.persistentDataPath;
        string[] userFiles = System.IO.Directory.GetFiles(userPath, "*.?8?", System.IO.SearchOption.TopDirectoryOnly);
        if (userFiles.Length > 0)
        {
            for (int i = 0; i < userFiles.Length; i++)
            {
                string diskPath = userFiles[i];
                string displayName = "User/" + System.IO.Path.GetFileName(userFiles[i]);

                DiskImage diskImage = new DiskImage();
                diskImage.filePath = diskPath;
                diskImage.displayName = displayName;
                diskImagesList.Add(diskImage);

                diskNameList.Add(displayName);
            }
        }

        UpdateDropdowns();

        InsertSY0();
        InsertSY1();
        InsertSY2();

        diskVol[0] = 0;
        diskVol[1] = 0;
        diskVol[2] = 0;
    }

    void UpdateDropdowns()
    {
        sy0Dropdown.ClearOptions();
        sy0Dropdown.AddOptions(diskNameList);
        sy1Dropdown.ClearOptions();
        sy1Dropdown.AddOptions(diskNameList);
        sy2Dropdown.ClearOptions();
        sy2Dropdown.AddOptions(diskNameList);
    }

    void InsertDisk(int drive, string diskName)
    {
        Debug.Log("InsertDisk() drive=" + drive.ToString() + " diskName=" + diskName);

        string path = diskName;
        byte[] buffer = System.IO.File.ReadAllBytes(path);

        if (buffer.Length == (2560 * 40))
        {
            diskSides[drive] = 1;
            diskNumTracks[drive] = 40;
        }
        else if (buffer.Length == (2560 * 40 * 2))
        {
            diskSides[drive] = 2;
            diskNumTracks[drive] = 40;
        }
        else if (buffer.Length == (2560 * 80 * 2))
        {
            diskSides[drive] = 2;
            diskNumTracks[drive] = 80;
        }

        Debug.Log("Loading image:" + path + " bytes=" + buffer.Length.ToString() + " sides=" + diskSides[drive].ToString());

        if (drive == 0)
        {
            System.Buffer.BlockCopy(buffer, 0, disk0Buffer, 0, buffer.Length);
        }
        else if (drive == 1)
        {
            System.Buffer.BlockCopy(buffer, 0, disk1Buffer, 0, buffer.Length);
        }
        else if (drive == 2)
        {
            System.Buffer.BlockCopy(buffer, 0, disk2Buffer, 0, buffer.Length);
        }

        isHDOS[0] = IsHDOSDisk(disk0Buffer);
        if (isHDOS[0])
        {
            diskVol[0] = disk0Buffer[0x900];
        }
        isHDOS[1] = IsHDOSDisk(disk1Buffer);
        if (isHDOS[1])
        {
            diskVol[1] = disk1Buffer[0x900];
        }
        isHDOS[2] = IsHDOSDisk(disk2Buffer);
        if (isHDOS[2])
        {
            diskVol[2] = disk2Buffer[0x900];
        }
    }

    public byte H17IOReadByte(int port)
    {
        byte b = 0;

        if (port == 0x7C)
        {
            b = ReadDataByte();
            syncDetect = false;
            return b;
        }

        if (port == 0x7D)
        {
            b = 0x80;
            if (dataAvail)
            {
                b |= 0x01;
            }
            return b;
        }

        if (port == 0x7E)
        {
            //Debug.Log("H17IOReadByte() port=" + port.ToString("X3"));
            syncDetect = true;
            return 0xFF;
        }

        if (port == 0x7F)
        {
            if (sectorBufferIndex < 5 || sectorBufferIndex == sectorBufferSize)
            {
                sectorBufferIndex = 0;
            }
			DiskSim(); // simulate disk rotation only when polling for status
            if (diskHole)
            {
                diskHole = false;
                b |= 0x01;
            }
            if (diskTrack[driveSelect] == 0)
            {
                b |= 0x02;
            }
            if (writeProtect)
            {
                b |= 0x04;
            }
            if (syncDetect)
            {
                b |= 0x08;
            }
            return b;
        }

        Debug.Log("H17IOReadByte() port=" + port.ToString("X3") + " Value=" + b.ToString("X2"));
        return b;
    }

    public void H17IOWriteByte(int port, byte c)
    {
        //Debug.Log("H17IOWriteByte() port=" + port.ToString("X") + " data=" + c.ToString("X") + " sectorBufferIndex=" + sectorBufferIndex.ToString());

        if (sectorBufferIndex < 5 || sectorBufferIndex == sectorBufferSize)
        {
            sectorBufferIndex = 0;
        }

        if (port == 0x7C)
        {
			int n = 0;
            if (writeGateEnable)
            {
                if (c == 0xFD && syncCharCount == 0) // wait for sync char before writing data
                {
                    syncCharCount++;
					writeCount = 0;
                    canWrite = true;
                }
				
				if (canWrite)
                {
                    int hdrCharCount = 1;
					n = writeCount++;
                    if (n < hdrCharCount) // 0xFD,VOL,TRK,SEC,CHK,0xFD
                    {
                        // ignore sector header and sync char
                        //Debug.Log("H17IOWriteByte() vol=" + diskVol[driveSelect].ToString() + " trk=" + diskTrack[driveSelect].ToString() + " sec=" + diskSector.ToString() + " port=" + port.ToString("X3") + " c=" + c.ToString("X2"));
                    }
                    else if (n < 256 + hdrCharCount)
                    {
                        diskBuffer[writeBufferIndex++] = c;
                    }
                    else
                    {
                        // finished writing out data so ignore rest of data bytes
                        syncCharCount = 0;
                        canWrite = false;
                    }
                }

				n = writeBufferCount % trackWriteBuffer.Length;
				trackWriteBuffer[n] = c;
                trackSectorMarker[n] = 0;
                if ((n % 320) == 0)
                {
                    trackSectorMarker[n] = 1;
                }
				writeBufferCount++;
                return;
            }
        }

        if (port == 0x7E)
        {
            return;
        }

        if (port == 0x7F)
        {
            if ((c & 0x01) != 0)
            {
                if (!writeGateEnable)
                {
                    writeGateEnable = true;
					writeBufferCount = 0;
                }
            }
            else
            {
                if (writeGateEnable)
                {
                    writeGateEnable = false;
					canWrite = false;
					writeCount = 0;
				}
			}

            if ((c & 0x02) != 0)
            {
                driveSelect = 0;
                diskBuffer = disk0Buffer;
            }
            else if ((c & 0x04) != 0)
            {
				driveSelect = 1;
                diskBuffer = disk1Buffer;
            }
            else if ((c & 0x08) != 0)
            {
				driveSelect = 2;
                diskBuffer = disk2Buffer;
            }

            if ((c & 0x10) != 0)
            {
                motorOn = true;
            }
            else
            {
                motorOn = false;
            }

            if ((c & 0x20) != 0)
            {
                stepDirection = 1;
            }
            else
            {
                stepDirection = 0;
            }

            if ((c & 0x40) != 0)
            {
                if (stepDirection != 0)
                {
                    diskTrack[driveSelect]++;
                }
                else
                {
                    diskTrack[driveSelect]--;
                }
                diskTrack[driveSelect] = Mathf.Clamp(diskTrack[driveSelect], 0, diskNumTracks[driveSelect]);
            }

            if ((c & 0x80) != 0)
            {
                writeEnable = true;
            }
            else
            {
                writeEnable = false;
            }

            return;
        }

        Debug.Log("H17IOWriteByte() port=" + port.ToString("X3") + " Value=" + c.ToString("X2"));
        portAdr[port] = c;
    }

    byte[] sectorHeader = new byte[5];
	string sectorString;

	byte ReadDataByte()
	{
		byte b = 0;

		int n = sectorBufferIndex++;
		b = sectorBuffer[n];
		/* keep this for debugging
		if (n < 5) // 0xFD,vol,trk,sec,chk
		{
			sectorHeader[n] = b;
		}
		if (n == 5)
		{
			int side = (H89.Instance.h89IO.sideSelect) ? 1 : 0;
			Debug.Log("ReadDataByte() header passed SY" + driveSelect.ToString() + " side=" + side.ToString() + " header vol=" + sectorHeader[1].ToString() + " trk=" + sectorHeader[2].ToString() + "/" + diskTrack[driveSelect].ToString() + " sec=" + sectorHeader[3].ToString() + " offset=" + writeBufferIndex.ToString("X8"));
			sectorString = string.Empty;
		}
		if (n > 5)
		{
			sectorString += b.ToString("X2") + " ";
			int i = n - 6;
			if (i == 256)
			{
				Debug.Log("drive=" + driveSelect.ToString() + " track=" + diskTrack[driveSelect].ToString() + " sector=" + diskSector.ToString() + ":" + sectorString);
			}
		}
		//*/
		return b;
	}

    void ComputeChecksum(byte c)
    {
        checksum ^= c;
        checksum <<= 1;
        if ((checksum & 0x0100) != 0)   //  went through the carry-bit, wrap back to bit 1
        {
            checksum = (checksum & 0xFF) | 0x01;
        }
    }

    void DiskSim()
    {
        if (motorOn)
        {
			long ms = H89.Instance.stopWatch.ElapsedMilliseconds;
            ulong tstates = H89.Instance.accumulatedStates;
			ulong t = statesPerHole;
            if (holeCount == 9 || holeCount == 10)
            {
                t /= 2;
            }
            ulong n = lastHoleTime + t;
            if (tstates >= n)
            {
				H89Timing.Instance.holeTiming[holeCount] = tstates - lastHoleTime;
				H89Timing.Instance.holeTimingMs[holeCount] = (ulong)ms - lastHoleTicks;
                lastHoleTime = tstates;
				lastHoleTicks = (ulong)ms;
                if (holeCount < 10)
                {
					SetSector(holeCount);
                }
                holeCount = (holeCount + 1) % 11;
                diskHole = true;
            }
			// simulate over-hole milliseconds - not really needed
			//if (diskHole)
			//{
			//	if (tstates >= lastHoleTime + statesOverHole)
			//	{
			//		H89Timing.Instance.overHoleMs = ms - lastHoleTicks;
			//		diskHole = false;
			//	}
			//}
        }
    }

    public static void ResetSystem()
    {
        Instance.lastHoleTime = 0;
        Instance.lastHoleTicks = 0;
        Instance.holeCount = 0;
        Instance.diskHole = false;
    }

    void SetSector(int n)
	{
		diskSector = n;
		BuildSectorBuffer();
	}

	public void OnFetch(ulong tstates)
    {
    }

    int GetDiskBufferIndex(int t, int s)
    {
        // int t = (diskTrack[driveSelect] * diskSides[driveSelect]) + side;
        int offset = (2560 * t) + (s * 256);
        return offset;
    }

    void BuildSectorBuffer()
    {
        int k = 0;
        byte c = 0xFD;
        checksum = c;
        sectorBuffer[k++] = c; // sync char
        ComputeChecksum(c);

        int side = (H89.Instance.h89IO.sideSelect) ? 1 : 0;
		int t = (diskTrack[driveSelect] * diskSides[driveSelect]) + side;
        int vol = (t == 0) ? 0 : diskVol[driveSelect];
        c = (byte)vol;
        sectorBuffer[k++] = c; // volume num
        ComputeChecksum(c);

        c = (byte)t;
        sectorBuffer[k++] = c; // track num
        ComputeChecksum(c);

        c = (byte)diskSector;
        sectorBuffer[k++] = c; // sector num
        ComputeChecksum(c);

        c = (byte)checksum;
        sectorBuffer[k++] = c; // checksum

		//Debug.Log("BuildSectorBuffer() drive=" + driveSelect.ToString() + " vol=" + vol.ToString() + " track=" + t.ToString() + " sector=" + diskSector.ToString());

        // beginning of data
        c = 0xFD;
        checksum = c;
        sectorBuffer[k++] = c; // sync char
        ComputeChecksum(c);

        int offset = GetDiskBufferIndex(t, diskSector);
        for (int i = 0; i < 256; i++)
        {
            c = diskBuffer[offset + i];
            sectorBuffer[k++] = c; // data byte
            ComputeChecksum(c);
        }
        c = (byte)checksum;
        sectorBuffer[k++] = c; // checksum
        sectorBufferSize = k;
        sectorBufferIndex = 0;
        writeBufferIndex = offset;
    }

	public void Disassembly()
    {
        diskHoleText.text = "SECTOR=" + diskSector.ToString() + " HOLE=" + (diskHole ? "1" : "0");
    }

    public bool IsHDOSDisk(byte[] track_buffer)
    {
        if ((track_buffer[0] == 0xAF && track_buffer[1] == 0xD3 && track_buffer[2] == 0x7D && track_buffer[3] == 0xCD) ||   //  V1.x
            (track_buffer[0] == 0xC3 && track_buffer[1] == 0xA0 && track_buffer[2] == 0x22 && track_buffer[3] == 0x20) ||   //  V2.x
            (track_buffer[0] == 0xC3 && track_buffer[1] == 0xA0 && track_buffer[2] == 0x22 && track_buffer[3] == 0x30) ||   //  V3.x
            (track_buffer[0] == 0xC3 && track_buffer[1] == 0x1D && track_buffer[2] == 0x24 && track_buffer[3] == 0x20) ||   //  V? Super-89
            (track_buffer[0] == 0x18 && track_buffer[1] == 0x1E && track_buffer[2] == 0x13 && track_buffer[3] == 0x20) ||   //  OMDOS
            (track_buffer[0] == 0xC3 && track_buffer[1] == 0xD1 && track_buffer[2] == 0x23 && track_buffer[3] == 0x20))     //  OMDOS
        {
            return true;
        }
        return false;
    }

    public void InsertSY0()
    {
        int idx = sy0Dropdown.value;
        InsertSYx(idx, 0);
    }

    public void SaveSY0()
    {
        SaveSYx(0);
    }

    public void InsertSY1()
    {
        int idx = sy1Dropdown.value;
        InsertSYx(idx, 1);
    }

    public void SaveSY1()
    {
        SaveSYx(1);
    }

    public void InsertSY2()
    {
        int idx = sy2Dropdown.value;
        InsertSYx(idx, 2);
    }

    public void SaveSY2()
    {
        SaveSYx(2);
    }

    void InsertSYx(int idx, int drive)
    {
        if (idx > 0)
        {
            string diskPath = diskImagesList[idx].filePath;
            diskFileName[drive] = System.IO.Path.GetFileName(diskPath);
            InsertDisk(drive, diskPath);
        }
        else
        {
            if (drive == 0)
            {
                System.Array.Clear(disk0Buffer, 0, disk0Buffer.Length);
            }
            else if (drive == 1)
            {
                System.Array.Clear(disk1Buffer, 0, disk1Buffer.Length);
            }
            else
            {
                System.Array.Clear(disk2Buffer, 0, disk2Buffer.Length);
            }
            diskFileName[drive] = string.Empty;
            diskNumTracks[drive] = 40;
        }
    }

    void SaveSYx(int drive)
    {
        if (string.IsNullOrEmpty(diskFileName[drive]))
        {
            return;
        }
        string path = Application.persistentDataPath;
        string filePath = System.IO.Path.Combine(path, diskFileName[drive]);

        SaveDiskImage(drive, filePath);
    }

    void SaveDiskImage(int drive, string filePath)
    {
        byte[] buffer = null;
        if (diskSides[drive] == 1)
        {
            // single sided
            if (diskNumTracks[drive] == 40)
            {
                buffer = new byte[2560 * 40];
            }
        }
        else
        {
            // double sided
            if (diskNumTracks[drive] == 40)
            {
                buffer = new byte[2560 * 40 * 2];
            }
            else
            {
                buffer = new byte[2560 * 80 * 2];
            }
        }

        Debug.Log("SaveDiskImage() filePath=" + filePath + " length=" + buffer.Length.ToString());

        if (drive == 0)
        {
            System.Buffer.BlockCopy(disk0Buffer, 0, buffer, 0, buffer.Length);
        }
        else if (drive == 1)
        {
            System.Buffer.BlockCopy(disk1Buffer, 0, buffer, 0, buffer.Length);
        }
        else
        {
            System.Buffer.BlockCopy(disk2Buffer, 0, buffer, 0, buffer.Length);
        }

        System.IO.File.WriteAllBytes(filePath, buffer);

        string displayName = "User/" + System.IO.Path.GetFileName(filePath);
        if (diskNameList.Contains(displayName))
        {
            return;
        }

        DiskImage diskImage = new DiskImage();
        diskImage.filePath = filePath;
        diskImage.displayName = displayName;
        diskImagesList.Add(diskImage);

        diskNameList.Add(displayName);

        UpdateDropdowns();
    }
}
