using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class H89Timing : MonoBehaviour
{
	public long clockMs;
	public ulong[] holeTiming;
	public ulong[] holeTimingMs;
	public bool motorOn;
	public int driveSelect;
	public int[] diskTrack;
	public int diskSector;
	public int holeCount;
	public long overHoleMs;

	public static H89Timing Instance;

	void Awake()
	{
		Instance = this;
	}
}
