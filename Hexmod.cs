/*
 * Because it is based on reverse-engineering the Unity
 * assembly (extracted through dnSpy), this mod's source code
 * is a little spaghetti, but I have tried to make it readable.
 *
 * See README.MD for more info.
 * 
 * Author: /u/ShiningConcepts
 */

using System;
using System.Reflection;
using BepInEx;
using UnityEngine;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;

namespace Hexmod
{
	[BepInPlugin(pluginGuid, pluginName, pluginVersion)]
	public class Hexmod : BaseUnityPlugin
	{
		//BepInEx identifiers.
		public const string pluginGuid = "shiningconcepts.hexmod";
		public const string pluginName = "Hexmod";
		public const string pluginVersion = "1.0";

		//This array keeps track of all cells that have been
		//marked blue, so that the hint updating
		//algorithm knows to exclude them.
		public static bool[,] foundArray = new bool[34, 34];

		//Function for logging messages.
		public static void Log(String m)
		{
			var l = BepInEx.Logging.Logger.CreateLogSource("Hexmod");
			l.LogInfo(m);
		}

		//Function that runs on mod startup. Applies patches.
		public void Awake()
		{
			Log("Hexmod loaded!");

			//Harmony is used to patch methods.
			Harmony h = new Harmony(pluginGuid);

			//First, patch the function that loads the leevel, so
			//that cells marked as blue at the start are accounted for.
			h.Patch(AccessTools.Method(typeof(LevelCompleteScript), "Start"),
			null,
			new HarmonyMethod(AccessTools.Method(typeof(Hexmod), "MarkDefaultBlues")));

			//Now, patch the function that loads blue cells
			//from a loaded save state, and account for those cells.
			h.Patch(AccessTools.Method(typeof(HexBehaviour),
				"QuickHighlightClick", new Type[] { typeof(int), typeof(int) }),
			new HarmonyMethod(AccessTools.Method(typeof(Hexmod), "QuickBlueReact")));

			//Finally, patch the function triggered when blue cells are manually
			//highlighted by the player to account for them.
			h.Patch(
			AccessTools.Method(typeof(HexBehaviour), "HighlightClick"),
			new HarmonyMethod(AccessTools.Method(typeof(Hexmod), "BlueReact")));
		}

		//MarkDefaultBlues() will catch the cells displayed as blue by default.
		public static void MarkDefaultBlues()
		{
			/*
			 * From my debugging with Unity Inspector:
			 * All orange cells active are in Hex Grid Overlay.
			 * All blue cells (found or not) are in Hex Grid.
			 * 
			 * So, we get all blue cells from Hex Grid.
			 * We then eliminate the blue cells that are unfound
             * (have a corresponding orange) in Hex Grid Overlay.
			 * 
			 * We then account for the ones that remain.
			 * */

			//Since this function is run first, we first clear foundArray.
			foundArray = new bool[34, 34];

			List<String> l = new List<String>();

			//List all Blue cells.
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

			if (l.Count == 0)
				return;

			//Now, for each found blue tile, mark it in foundArray, and
			//update the corresponding hints.
			GameObject[,] array = getArrayOfTiles();
			foreach (string s in l)
			{
				int a = Int32.Parse(s.Split('-')[0]);
				int b = Int32.Parse(s.Split('-')[1]);

				updateAllHints(a, b);
			}
		}

		//React to non-default blues from a save state.
		public static void QuickBlueReact(HexBehaviour __instance)
		{
			//Mark the cell as correct.
			int a = Mathf.RoundToInt(__instance.gameObject.transform.position.x / 0.88f) + 16;
			int b = Mathf.RoundToInt(__instance.gameObject.transform.position.y / 0.5f) + 16;

			updateAllHints(a, b);
		}

		//React to manual clicks.
		public static void BlueReact(HexBehaviour __instance)
		{
			//First, ensure the user is actually trying to mark a blue cell as blue.
			if (!(__instance.containsShapeBlock))
				return;

			//Now, do the same thing done in the code for quick reactions.
			QuickBlueReact(__instance);
		}

		//Update hints and first mark a cell as blue.
		public static void updateAllHints(int a, int b)
		{
			foundArray[a, b] = true;
			GameObject[,] array = getArrayOfTiles();
			updateBlackHexHints(array);
			updateFlowerNumbers(array);
			updateLineHints(array);
		}

		//The rest of the code is primarily based on reverse-engineering of the
		//code found when decompiling EditorFunctions.GenerateHexNumbers().

		//Generate an array of tiles needed by the below functions.
		//This array can map integers to specific tiles.
		public static GameObject[,] getArrayOfTiles()
		{
			GameObject[,] array = new GameObject[34, 34];
			IEnumerator enumerator = GameObject.Find("Hex Grid").transform.GetEnumerator();
			while (enumerator.MoveNext())
			{
				object obj = enumerator.Current;
				Transform t = (Transform)obj;
				int a = Mathf.RoundToInt(t.position.x / 0.88f) + 16;
				int b = Mathf.RoundToInt(t.position.y / 0.5f) + 16;
				array[a, b] = t.gameObject;
			}
			return array;
		}

		//Update the hints shown on black cells.
		public static void updateBlackHexHints(GameObject[,] array)
		{
			for (int i = 0; i <= 33; i++)
			{
				for (int j = 0; j <= 33; j++)
				{
					if (array[i, j] == null)
						continue;

					//This check will exclude black hexes with "?" (undisclosed neighbor count).
					if (!(array[i, j].name == "Black Hex" || array[i, j].name == "Black Hex(Clone)"))
						continue;

					int blueNeighborCount = 0;

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
					int a, b;
					for (int x = 0; x < pairs.Length; x += 2)
					{
						a = pairs[x];
						b = pairs[x + 1];

						//Make sure each indice is valid ([0, 33]), then
						//ensure the tile is non-null (exists), is blue,
						//and exclude tiles that are already found.

						if (0 > a || 0 > b || 33 < a || 33 < b)
							continue;
						if (array[a, b] == null)
							continue;
						if (array[a, b].tag != "Blue")
							continue;
						if (foundArray[a, b])
							continue;

						blueNeighborCount++;
					}

					//Update the tile's hint text. Preserve the sequential/non-sequential text formatting if needed.
					if (array[i, j].tag == "Clue Hex (Sequential)" || array[i, j].tag == "Clue Hex (Sequential) Hidden")
					{
						array[i, j].transform.Find("Hex Number").GetComponent<TextMesh>().text = "{" + blueNeighborCount + "}";
					}
					else if (array[i, j].tag == "Clue Hex (NOT Sequential)" || array[i, j].tag == "Clue Hex (NOT Sequential) Hidden")
					{
						array[i, j].transform.Find("Hex Number").GetComponent<TextMesh>().text = "-" + blueNeighborCount + "-";
					}
					else
					{
						array[i, j].transform.Find("Hex Number").GetComponent<TextMesh>().text = blueNeighborCount.ToString();
					}
				}
			}
		}

		//Update the hint on flower cells.
		public static void updateFlowerNumbers(GameObject[,] array)
		{
			for (int i = 0; i <= 33; i++)
			{
				for (int j = 0; j <= 33; j++)
				{
					if (array[i, j] == null)
						continue;

					if (!(array[i, j].name == "Blue Hex (Flower)" || array[i, j].name == "Blue Hex (Flower)(Clone)"))
						continue;

					String original = array[i, j].transform.Find("Hex Number").GetComponent<TextMesh>().text;

					int blueNeighborCount = 0;

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
					int a, b;
					for (int x = 0; x < pairs.Length; x += 2)
					{
						a = pairs[x];
						b = pairs[x + 1];

						if (0 > a || 0 > b || 33 < a || 33 < b)
							continue;
						if (array[a, b] == null)
							continue;
						if (array[a, b].tag != "Blue")
							continue;
						if (foundArray[a, b])
							continue;

						blueNeighborCount++;

					}
					array[i, j].transform.Find("Hex Number").GetComponent<TextMesh>().text = blueNeighborCount.ToString();

				}
			}
		}

		//Update the line hints.
		public static void updateLineHints(GameObject[,] array)
		{
			//This is in the source code, I'm quite sure it's
			//unneeded here but included for safety.
			if (GameObject.Find("Columns Parent") == null)
				return;

			IEnumerator e2 = GameObject.Find("Columns Parent").transform.GetEnumerator();
			while (e2.MoveNext())
			{
				object o = e2.Current;
				Transform t = (Transform)o;

				String original = t.GetComponent<TextMesh>().text;

				//This is how the type of line (left/right/vertical) is computed.
				//This is not from reverse-engineering, it was found througgh debugging
				//with UnityInspector, the intuitive way of checking diagonal status
				//doesn't seem to work here.
				int ang = (int)t.GetChild(0).eulerAngles.y;
				String angle;
				if (ang == 180)
					angle = "Vertical";
				else if (ang == 270)
					angle = "Left";
				else if (ang == 90)
					angle = "Right";
				else
				{
					t.GetComponent<TextMesh>().text = "ERR";
					Log("ERROR - one of the column hints had an unexpected angle of " + ang);
					continue;
				}

				int a = Mathf.RoundToInt(t.position.x / 0.88f) + 16;
				int b = Mathf.RoundToInt(t.position.y / 0.5f) + 16;

				array[a, b] = t.gameObject;

				int blueCount = 0;

				//Now, for each direction, count how many blue cells
				//that are not found are in the line. (The logic for this
				//part is purely borrowed from the assembly.)

				if (angle == "Left")
				{
					int c = b - 1;

					for (int k = a - 1; k >= 0; k--)
					{
						if (array[k, c] != null && array[k, c].tag == "Blue" && !(foundArray[k, c]))
							blueCount++;
						c--;
						if (c < 0)
							break;
					}
				}

				else if (angle == "Right")
				{
					int c = b - 1;

					for (int l = a + 1; l < 31; l++)
					{
						if (array[l, c] != null && array[l, c].tag == "Blue" && !(foundArray[l, c]))
							blueCount++;
						c--;
						if (c < 0)
							break;
					}
				}

				else
				{
					for (int m = b - 2; m >= 0; m--)
					{
						if (array[a, m] != null && array[a, m].tag == "Blue" && !(foundArray[a, m]))
							blueCount++;
					}
				}

				//Now, update the text. Preserve sequential/non-sequential formatting.
				if (t.GetComponent<TextMesh>().text.Contains("{"))
					t.GetComponent<TextMesh>().text = "{" + blueCount + "}";
				else if (t.GetComponent<TextMesh>().text.Contains("-"))
					t.GetComponent<TextMesh>().text = "-" + blueCount + "-";
				else
					t.GetComponent<TextMesh>().text = blueCount.ToString();
			}
		}

	}
}