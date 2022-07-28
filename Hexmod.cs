//This is the source code for Hexmod, a QOL mod for Hexcells.
//This source code is largely based on reverse-engineering
//the Unity assembly (extracted through dnSpy).
//See README.MD for more info. Author: /u/ShiningConcepts

using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using UnityEngine;
using HarmonyLib;

namespace Hexmod
{
	[BepInPlugin(pluginGuid, pluginName, pluginVersion)]
	public class Hexmod : BaseUnityPlugin
	{
		//BepInEx identifiers.
		public const string pluginGuid = "shiningconcepts.hexmod";
		public const string pluginName = "Hexmod";
		public const string pluginVersion = "1.0";

		//This array can be used to mark indices to cells on the grid.
		public static GameObject[,] cellArray;

		//This array keeps track of all cells that have been
		//marked blue, so that they can be excluded
		//when counting unmarked blue cells.
		public static bool[,] foundArray = new bool[34, 34];

		//Function for logging messages.
		public static void Log(String m)
		{ BepInEx.Logging.Logger.CreateLogSource("Hexmod").LogInfo(m); }

		//Function that runs on mod startup. Applies patches.
		public void Awake()
		{
			Log("Hexmod loaded!");

			//Harmony is used to patch methods.
			Harmony h = new Harmony(pluginGuid);

			//First, patch the function that loads the level, so
			//that cells marked as blue at the start of the level are accounted for.
			//(The second parameter being null makes this a postfix rather than prefix.)
			h.Patch(AccessTools.Method(typeof(LevelCompleteScript), "Start"), null,
			new HarmonyMethod(AccessTools.Method(typeof(Hexmod), "MarkDefaultBlues")));

			//Now, patch the function that loads blue cells
			//from a loaded save state, so we can account for those cells.
			h.Patch(AccessTools.Method(typeof(HexBehaviour), "QuickHighlightClick", new Type[] { typeof(int), typeof(int) }),
			new HarmonyMethod(AccessTools.Method(typeof(Hexmod), "QuickBlueReact")));

			//Finally, patch the function triggered when blue cells are manually
			//highlighted by the player, to account for them.
			h.Patch(AccessTools.Method(typeof(HexBehaviour), "HighlightClick"),
			new HarmonyMethod(AccessTools.Method(typeof(Hexmod), "BlueReact")));
		}

		//MarkDefaultBlues() will catch the cells displayed as blue by default.
		public static void MarkDefaultBlues()
		{
			//MarkDefaultBlues() will always run as soon as a level is loaded (including
			//restarts). Therefore, we should reset foundArray at the start of it,
			//and also initialize the cellArray for the level.
			foundArray = new bool[34, 34];
			updateCellArray();

			/*
			 * From my debugging with Unity Inspector:
			 * All orange cells active are in Hex Grid Overlay.
			 * All blue cells (found or not) are in Hex Grid.
			 * 
			 * So, we get all blue cells from Hex Grid.
			 * We then eliminate the blue cells that are unfound
             * (have a corresponding orange) in Hex Grid Overlay.
			 * 
			 * What remains must be the cells marked as blue by default.
			 * */

			//So first, get all blue cells in Hex Grid.
			List<String> l = new List<String>();
			IEnumerator e = GameObject.Find("Hex Grid").transform.GetEnumerator();
			while (e.MoveNext())
			{
				object o = e.Current;
				Transform t = (Transform)o;
				if (t.tag == "Blue")
				{
					//This code works because the position of a cell
					//in Hex Grid is the same as its corresponding
					//orange cell in Hex Grid Overlay.
					int a = Mathf.RoundToInt(t.position.x / 0.88f) + 16;
					int b = Mathf.RoundToInt(t.position.y / 0.5f) + 16;
					l.Add(a + "-" + b);
				}
			}

			//Now, remove all Blue cells that have a corresponding Orange cell.
			e = GameObject.Find("Hex Grid Overlay").transform.GetEnumerator();
			while (e.MoveNext())
			{
				object o = e.Current;
				Transform t = (Transform)o;

				int a = Mathf.RoundToInt(t.position.x / 0.88f) + 16;
				int b = Mathf.RoundToInt(t.position.y / 0.5f) + 16;

				l.Remove(a + "-" + b);
			}

			//Now, for each remaining blue tile, mark it as found.
			foreach (string s in l)
			{
				int a = Int32.Parse(s.Split('-')[0]);
				int b = Int32.Parse(s.Split('-')[1]);
				markAsFound(a, b);
			}
		}

		//Account for cells marked as blue upon loading a save state.
		public static void QuickBlueReact(HexBehaviour __instance)
		{
			//Extract the indices representing the position of the cell on the grid.
			int a = Mathf.RoundToInt(__instance.gameObject.transform.position.x / 0.88f) + 16;
			int b = Mathf.RoundToInt(__instance.gameObject.transform.position.y / 0.5f) + 16;

			//Mark the cell as found and update the hints.
			markAsFound(a, b);
		}

		//React to manual clicks.
		public static void BlueReact(HexBehaviour __instance)
		{
			//First, ensure that the cell the user is trying to mark as blue is actually blue.
			if (!(__instance.containsShapeBlock))
				return;

			//Now we can just do the same thing done in QuickBlueReact().
			QuickBlueReact(__instance);
		}

		//Mark a cell as found in foundArray, and then update all hint types.
		public static void markAsFound(int a, int b)
		{
			foundArray[a, b] = true;
			updateBlackHexHints();
			updateFlowerNumbers();
			updateLineHints();
		}

		//Update a hint in consideration of its type
		public static string updateHint(String old, int n)
        {
			if (old.Contains("{")) //Sequential hint
				return "{" + n + "}";
			else if (old.Contains("-")) //Non-sequential hint
				return "-" + n + "-";
			return n.ToString();
        }

		//The rest of the code is primarily based on reverse-engineering of the
		//code found when decompiling EditorFunctions.GenerateHexNumbers().

		//Generate an array of tiles needed by the below functions.
		//This array can map integers to specific tiles.
		public static void updateCellArray()
		{
			cellArray = new GameObject[34, 34];
			IEnumerator e = GameObject.Find("Hex Grid").transform.GetEnumerator();
			while (e.MoveNext())
			{
				object obj = e.Current;
				Transform t = (Transform)obj;
				int a = Mathf.RoundToInt(t.position.x / 0.88f) + 16;
				int b = Mathf.RoundToInt(t.position.y / 0.5f) + 16;
				cellArray[a, b] = t.gameObject;
			}
		}

		//Update the hints shown on black cells.
		public static void updateBlackHexHints()
		{
			for (int i = 0; i <= 33; i++)
			{
				for (int j = 0; j <= 33; j++)
				{
					if (cellArray[i, j] == null)
						continue;

					if (!(cellArray[i, j].name == "Black Hex" || cellArray[i, j].name == "Black Hex(Clone)"))
						continue;
					if (cellArray[i, j].tag == "Clue Hex Blank" || cellArray[i, j].tag == "Clue Hex Blank Hidden")
						continue;

					int blueCount = 0;

					//The indices of all possible places on the grid a blue neighbor may be.
					var pairs = new int[]
					{
						i-1, j+1,
						i-1, j-1,
						i, j+2,
						i, j-2,
						i+1, j+1,
						i+1, j-1,
					};

					//Iterate over them two at a time.
					for (int x = 0; x < pairs.Length; x += 2)
					{
						int a = pairs[x];
						int b = pairs[x + 1];

						//Make sure each indice is valid ([0, 33]), is
						//non-null (exists in the level), is blue,
						//and exclude tiles that are already found.
						if (0 > a || 0 > b || 33 < a || 33 < b)
							continue;
						if (cellArray[a, b] == null)
							continue;
						if (cellArray[a, b].tag != "Blue")
							continue;
						if (foundArray[a, b])
							continue;

						blueCount++;
					}

					//Update the tile's hint text. Preserve the sequential/non-sequential text formatting if needed.
					cellArray[i, j].transform.Find("Hex Number").GetComponent<TextMesh>().text =
					updateHint(cellArray[i, j].transform.Find("Hex Number").GetComponent<TextMesh>().text, blueCount);
				}
			}
		}

		//Update the hints on flower cells.
		public static void updateFlowerNumbers()
		{
			for (int i = 0; i <= 33; i++)
			{
				for (int j = 0; j <= 33; j++)
				{
					if (cellArray[i, j] == null)
						continue;

					if (!(cellArray[i, j].name == "Blue Hex (Flower)" || cellArray[i, j].name == "Blue Hex (Flower)(Clone)"))
						continue;

					int blueCount = 0;

					//All possible indices of a cell in the region covered by a flower.
					var pairs = new int[]
					{
						i-1, j+1,
						i-1, j-1,
						i, j+2,
						i, j-2,
						i+1, j+1,
						i+1, j-1,
						i, j+4,
						i+1, j+3,
						i+2, j+2,
						i+2, j,
						i+2, j-2,
						i+1, j-3,
						i, j-4,
						i-1, j-3,
						i-2, j-2,
						i-2, j,
						i-2, j+2,
						i-1, j+3
					};

					for (int x = 0; x < pairs.Length; x += 2)
					{
						int a = pairs[x];
						int b = pairs[x + 1];

						if (0 > a || 0 > b || 33 < a || 33 < b)
							continue;
						if (cellArray[a, b] == null)
							continue;
						if (cellArray[a, b].tag != "Blue")
							continue;
						if (foundArray[a, b])
							continue;

						blueCount++;
					}
					cellArray[i, j].transform.Find("Hex Number").GetComponent<TextMesh>().text =
					updateHint(cellArray[i, j].transform.Find("Hex Number").GetComponent<TextMesh>().text, blueCount);
				}
			}
		}

		//Update the line hints.
		public static void updateLineHints()
		{
			if (GameObject.Find("Columns Parent") == null)
				return;

			IEnumerator e = GameObject.Find("Columns Parent").transform.GetEnumerator();
			while (e.MoveNext())
			{
				object o = e.Current;
				Transform t = (Transform)o;

				int a = Mathf.RoundToInt(t.position.x / 0.88f) + 16;
				int b = Mathf.RoundToInt(t.position.y / 0.5f) + 16;
				cellArray[a, b] = t.gameObject;

				//Use euler angles to compute the direction of the line.
				int xAng = (int)t.GetChild(0).eulerAngles.x;
				int yAng = (int)t.GetChild(0).eulerAngles.y;

				int xChange;
				int yChange;

				if (yAng == 180) //Straight line, count down
                {
					xChange = 0;
					yChange = -1;
                }
				else if (xAng == 330 && yAng == 90) //Up-right
                {
					xChange = 1;
					yChange = 1;
                }
				else if (xAng == 330 && yAng == 270) //Up-left
                {
					xChange = -1;
					yChange = 1;
                }
				else if (xAng == 29 && yAng == 90) //Down-right
                {
					xChange = 1;
					yChange = -1;
                }
				else if (xAng == 29 && yAng == 270) //Down-left
                {
					xChange = -1;
					yChange = -1;
                }
				else if (xAng == 0 && yAng == 90) //Rightwards
                {
					xChange = 1;
					yChange = 0;
                }
				else
				{
					t.GetComponent<TextMesh>().text = "ERR";
					Log("ERROR - one of the column hints had an unexpected angle of " + xAng + ", " + yAng);
					continue;
				}

				//Count blue cells in the line.
				int x = a + xChange;
				int y = b + yChange;
				if (yAng == 180) //For straight line, y must be initialized to y-2
					y--;

				int blueCount = 0;
				while (x >= 0 && y >= 0 && x <= 31 && y <= 31)
                {
					if (cellArray[x, y] != null && cellArray[x, y].tag == "Blue" && !(foundArray[x, y]))
						blueCount++;

					x += xChange;
					y += yChange;
                }

				//Now, update the text. Preserve sequential/non-sequential formatting.
				t.GetComponent<TextMesh>().text =
				updateHint(t.GetComponent<TextMesh>().text, blueCount);
			}
		}
	}
}
