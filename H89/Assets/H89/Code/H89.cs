using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using Z80;

public class H89 : MonoBehaviour
{
    public int statesPerUpdate;
    public int statesPerInt1;
    public H19 h19Console;
    [HideInInspector]
    public bool running;
    [HideInInspector]
    public int resetState;

    [HideInInspector]
    public Z80Disassembler disassembler;
    [HideInInspector]
    public H89Memory memory;
    [HideInInspector]
    public H89IO h89IO;
    [HideInInspector]
    public uP Z80;
    [HideInInspector]
    public ulong accumulatedStates;

	private int breakPointPC;

    public UnityEngine.UI.Text[] debugTextArray;
    [HideInInspector]
    public int[] debugAdrArray;
    public UnityEngine.UI.Text regs1;
    public UnityEngine.UI.Text regs2;
    public UnityEngine.UI.Text regs3;
	public UnityEngine.UI.Text hlmem;
    public UnityEngine.UI.Text tstatesText;
    public UnityEngine.UI.Text millisecondsText;

    public GameObject canvasRoot;
    public GameObject diskImagesRoot;

    private int lastpc;
    private int steppc;
    private ulong int1tstates;
    private long lastms;
    private long lastmselapsed;
    private long lastts;
    private long lasttselapsed;
    public bool singleStep;
    private bool step;
    public GameObject debugModeObjectRoot;
    [HideInInspector]
    public bool logIOW;
    [HideInInspector]
    public bool logIOR;
    [HideInInspector]
    public bool logMEMW;
    [HideInInspector]
    public bool logMEMR;

    public byte[] portE8 = new byte[8];

    private Thread Z80Thread;
    private bool active;

    public System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();

    public static H89 Instance;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        memory = new H89Memory();
        h89IO = new H89IO();
        disassembler = new Z80Disassembler(memory);
        Z80 = new uP(memory, h89IO);
        Z80.OnFetch += Z80_OnFetch;
        memory.OnWrite += OnMemoryWrite;
        debugAdrArray = new int[debugTextArray.Length];

        h19Console.onConnect += OnConnect;

        active = true;
        Z80Thread = new Thread(new ThreadStart(Z80Exec));
    }

    void OnDestroy()
    {
        active = false;
        if (Z80Thread.IsAlive)
        {
            Z80Thread.Join();
            Z80Thread = null;
        }
    }

    void Update()
    {
        tstatesText.text = accumulatedStates.ToString();
        millisecondsText.text = lasttselapsed.ToString("D8") + " mselapsed=" + lastmselapsed.ToString("D4") + " ms=" + lastms.ToString();
        ShowDisassembly(Z80.Status.PC);

        for (int i = 0; i < portE8.Length; i++)
        {
            portE8[i] = h89IO.genericPort[0xE8 + i];
        }

        if (resetState == 2)
        {
            Z80.Reset();
            System.Array.Clear(memory.Raw, 0, memory.Raw.Length);
            memory.LoadROM();
            memory.LoadH17ROM();
            H19.ResetSystem();
            H17.ResetSystem();
            resetState = 0;
            OnConnect();
        }
    }

    void OnConnect()
    {
        Debug.Log("OnConnect()");
        if (!Z80Thread.IsAlive)
        {
            Z80Thread.Start();
        }
        running = true;

        canvasRoot.SetActive(true);
        ShowDiskImages();
    }

    void Z80Exec()
    {
        while (active)
        {
            if (running)
            {
                if (!stopWatch.IsRunning)
                {
                    stopWatch.Start();
                }
                int updateStates = statesPerUpdate;
                if (singleStep)
                {
                    updateStates = 0;
                    if (step)
                    {
                        if (steppc == -1)
                        {
                            steppc = Z80.Status.PC;
                        }
                        updateStates = 1;
                    }
                }
                if (updateStates > 0)
                {
                    Z80.tstates = 0;
                    Z80.event_next_event = updateStates;
                    Z80.Execute();
                    accumulatedStates += (ulong)Z80.tstates;
                    if (step && Z80.Status.PC != steppc)
                    {
                        steppc = -1;
                        step = false;
                    }
                }

                if (accumulatedStates >= int1tstates)
                {
                    long ms = stopWatch.ElapsedMilliseconds;
                    /*
                    lastmselapsed = ms - lastms;
                    lastms = ms;
                    lasttselapsed = Z80.tstates - lastts;
                    lastts = Z80.tstates;
                    */
                    H89Timing.Instance.clockMs = ms - lastms;
                    lastms = ms;
                    if (h89IO.intEnable)
                    {
                        if (Z80.Status.IFF1)
                        {
                            // clock interrupt
                            Z80.Status.I = 1;
                            Z80.Interrupt();
                        }
                    }

                    int1tstates = accumulatedStates + (ulong)statesPerInt1;
                }

                // keyboard interrupt
                if (h19Console.HasCharDirect())
                {
                    if ((h89IO.genericPort[0xE9] & 0x01) != 0)
                    {
                        h89IO.genericPort[0xEA] = 0x04;
                        if (Z80.Status.IFF1)
                        {
                            Z80.Status.I = 3;
                            Z80.Interrupt();
                        }
                    }
                }
            }

            if (resetState == 1)
            {
                resetState = 2;
            }
        }
    }

	private long next2MhzTick;

    void Z80_OnFetch()
    {
		if (force2Mhz)
		{
			while (stopWatch.ElapsedTicks < next2MhzTick)
			{
				// loop
			}
			next2MhzTick = stopWatch.ElapsedTicks + 50;
		}

		H17.Instance.OnFetch(accumulatedStates);

		if (breakPointPC != 0 && Z80.Status.PC == breakPointPC)
		{
			breakPointPC = 0;
			steppc = -1;
			step = false;
			SingleStepButton();
		}
    }

    void OnMemoryWrite(ushort adr, byte v)
    {
        if (logMEMW)
        {
            if (adr >= 0x201B && adr <= 0x201C)
            {
                return;
            }
            Debug.Log("OnWriteMemory() adr=" + adr.ToString("X4") + " v=" + v.ToString("X2"));
        }
    }

    void OnMemoryRead(ushort adr)
    {
        if (logMEMR)
        {
            byte v = memory.Raw[adr];
            Debug.Log("OnReadMemory() adr=" + adr.ToString("X4") + " v=" + v.ToString("X2"));
        }
    }

    public void SingleStepButton()
    {
        singleStep = (singleStep ? false : true);
        Z80.event_next_event = Z80.tstates;
    }

    public void StepButton()
    {
        step = true;
    }

    public void DebugModeToggle()
    {
        if (debugModeObjectRoot.activeInHierarchy)
        {
            debugModeObjectRoot.SetActive(false);
        }
        else
        {
            debugModeObjectRoot.SetActive(true);
        }
        singleStep = false;
        step = false;
        lastpc = -1;
    }

    string ToOctal(int n)
    {
        int hi = ((n >> 8) & 0xFF);
        int lo = (n & 0xFF);
        string s = ToOctalByte(hi) + "." + ToOctalByte(lo);
        return s;
    }

    string ToOctalByte(int c)
    {
        string s = c.ToString("X2");
        return s;
    }

    void ShowDisassembly(ushort pc)
    {
        int pctop = debugAdrArray[0];
        int pcbot = debugAdrArray[debugAdrArray.Length - 1];
        int origpc = pc;

        regs1.text = "AF=" + ToOctal(Z80.Status.AF) + " BC=" + ToOctal(Z80.Status.BC);
        regs2.text = "DE=" + ToOctal(Z80.Status.DE) + " HL=" + ToOctal(Z80.Status.HL);
        int clockms = memory.ReadWord(0x201B);
        regs3.text = "CLOCK=" + ToOctal(clockms);
		hlmem.text = "(HL)=" + memory.Raw[Z80.Status.HL].ToString("X2");
        if (pc < pctop || pc > pcbot)
        {
            for (int i = 0; i < debugTextArray.Length; i++)
			{
				if (breakPointPC == pc)
				{
					debugTextArray[i].color = Color.red;
				}
				else if (pc == origpc)
				{
					debugTextArray[i].color = Color.blue;
				}
				else
				{
					debugTextArray[i].color = Color.black;
				}
				debugAdrArray[i] = pc;
                debugTextArray[i].text = (pc == origpc) ? ">" : "-";
                debugTextArray[i].text += ToOctal(pc);
                string s = disassembler.Disassemble(ref pc);
                debugTextArray[i].text += " " + s;
			}
		}
        else
        {
            for (int i = 0; i < debugTextArray.Length; i++)
            {
                if (debugAdrArray[i] == pc || debugAdrArray[i] == lastpc)
                {
                    ushort dispc = (ushort)debugAdrArray[i];
					if (breakPointPC == dispc)
					{
						debugTextArray[i].color = Color.red;
					}
					else if (dispc == origpc)
					{
						debugTextArray[i].color = Color.blue;
					}
					else
					{
						debugTextArray[i].color = Color.black;
					}
					debugTextArray[i].text = (dispc == origpc) ? ">" : "-";
                    debugTextArray[i].text += ToOctal(dispc);
                    string s = disassembler.Disassemble(ref dispc);
                    debugTextArray[i].text += " " + s;
                }
            }
        }
        lastpc = origpc;

        H17.Instance.Disassembly();
    }

    public void LogIOWButton()
    {
        logIOW = (logIOW ? false : true);
    }

    public void LogIORButton()
    {
        logIOR = (logIOR ? false : true);
    }

    public void LogMEMWButton()
    {
        logMEMW = (logMEMW ? false : true);
    }

    public void LogMEMRButton()
    {
        logMEMR = (logMEMR ? false : true);
    }

	public void BreakPointButton(int button)
	{
		breakPointPC = debugAdrArray[button];
		singleStep = false;
	}

    private bool force2Mhz;
    private int oldClock;

    public void Force2MhzButton()
    {
        if (force2Mhz)
        {
            //statesPerInt1 = oldClock;
            force2Mhz = false;
        }
        else
        {
            //oldClock = statesPerInt1;
            //statesPerInt1 = 35000;
            force2Mhz = true;
        }
    }

    public void ShowDiskImages()
    {
        diskImagesRoot.SetActive(true);
    }

    public void HideDiskImages()
    {
        diskImagesRoot.SetActive(false);
    }

    public void ResetSystem()
    {
        resetState = 1;
        running = false;
    }
}
