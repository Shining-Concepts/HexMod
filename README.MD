# Hexmod

**Hexmod** is a QOL mod for the **Hexcells trilogy** ([Hexcells](https://store.steampowered.com/app/265890/Hexcells/), [Hexcells Plus](https://store.steampowered.com/app/271900/Hexcells_Plus/) and [Hexcells Infinite](https://store.steampowered.com/app/304410/Hexcells_Infinite/)).

The motivation for making this mod is as follows: there is a game that I prefer over the trilogy (that is very similar to them) called [Tametsi](https://store.steampowered.com/app/709920/Tametsi/). The reason I prefer Tametsi is because it has some very nice QOL features; namely, when a mine is discovered, the hints relevant to that mine count down. For example, if you have a line indicating that 3 of the tiles touching that line have a mine, then every time you find a mine in that line, the line's number decrements (and similarly for tile numbers indicating how many of the tile's neighbors are mines). This is extremely convenient because it cuts down on tedious and menial mental work of counting yourself how many mined neighbors are in the line or are being touched; it makes the game faster without directly making it harder.

I have made Hexmod to port this exact feature to the Hexcells games, and it is a success. The mod supports all hints available in the Hexcells trilogy; hints on black tiles, hints on columns, and hints on blue flowers. The mod is also compatible with resuming levels from a save and levels where some cells are marked blue at the beginning.

# Demonstration

Vanilla game:

![This is an image](Old.png)

Hexmod:

![This is an image](New.png)

# Installation

Due to their similar codebases, **the installation progress is the same for each Hexcells game**.

Download Hexmod from the releases page.

Place the contents inside the .zip (not the .zip itself, the contents inside it) next to the .exe file for any Hexcells game - which is the root of the installation directory. For example, the original Hexcells installation directory has the file "Hexcells.exe" in it and the folder "Hexcells_Data". After you install this mod, you should also have "doorstop_config.ini", "winhttp.dll" and a folder called "BepInEx" in there.

To delete this mod, simply delete those 2 files and the "BepInEx" folder.

# Thanks

In no particular order, I would like to thank:

* Matthew Brown for making these amazing puzzle games.

* The developers of the [BepInEx](https://github.com/BepInEx/BepInEx), [UnityInspector](https://github.com/Mohelm97/UnityInspector) and [dnSpy](https://github.com/dnSpy/dnSpy) programs. BepInEx was the framework for developing this mod, dnSpy helped me view the game's assembly code, and UnityInspector helped me inspect the game as it ran to gain useful information to inform my coding (without it, I would've had to give up at several points).

* Abbysssal on Steam for writing [this guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2106187116) which was extremely helpful and the foundation of my coding approach. Though the guide was specifically for Streets of Rogue I found it was great for learning BepInEx in general.

# How to Build

The first 3 steps of the linked Abbysssal Steam guide should get you started. The specific Visual Studio references I needed to add were 0Harmony.dll and BepInEx.dll from "BepInEx/Core" when you download BepInEx, as well as Assembly-CSharp.dll and UnityEngine.dll from "Hexcells\Hexcells_Data\Managed".

Afterwards, you can Build your solution. Protip: configure VS so that it automatically builds your solution to the "BepInEx/plugins" folder, as that folder is where your BepInEx mod will be stored. This will save you the time of manually copying and pasting the files.

# Troubleshooting

If you encounter any errors or unexpected behavior with this mod please let me know. You can reach out to me via email at reddit.shiningconcepts@gmail.com or directly on Reddit at /u/ShiningConcepts. Please detail what error you're having and steps to reproduce it.