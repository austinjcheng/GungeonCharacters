﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using StatType = PlayerStats.StatType;
using UnityEngine;
using GungeonAPI;
using Ionic.Zip;

namespace CustomCharacters
{
    /*
     * Loads all the character data from the characterdata.txt
     * and then ships it off to CharacterBuilder
     */
    public static class Loader
    {
        public static string CharacterDirectory = Path.Combine(ETGMod.ResourcesDirectory, "../CustomCharacterData/");
        public static string DataFile = "characterdata.txt";

        public static List<CustomCharacterData> characterData = new List<CustomCharacterData>();

        public static void Init()
        {
            try
            {
                if (!Directory.Exists(CharacterDirectory))
                {
                    DirectoryInfo dir = Directory.CreateDirectory(CharacterDirectory);
                    ETGModConsole.Log("Created directory: " + dir.FullName);
                }
            }
            catch (Exception e)
            {
                Tools.PrintError("Error creating custom character directory");
                Tools.PrintException(e);
            }

            LoadCharacterData();
            foreach (CustomCharacterData data in characterData)
            {
                bool success = true;
                try
                {
                    CharacterBuilder.BuildCharacter(data);
                }
                catch (Exception e)
                {
                    success = false;
                    Tools.PrintError("An error occured while creating the character: " + data.name);
                    Tools.PrintException(e);
                }
                if (success)
                    Tools.Print("Built prefab for: " + data.name);
            }
        }

        //Finds sprite folders, sprites, and characterdata.txt (and parses it)
        public static void LoadCharacterData()
        {
            LoadDirectories();
            LoadZips();
        }

        private static void LoadDirectories()
        {
            var directories = Directory.GetDirectories(CharacterDirectory);
            Tools.Print("# of character folders found: " + directories.Length);
            for (int i = 0; i < directories.Length; i++)
            {
                Tools.StartTimer("Loading data for " + Path.GetFileName(directories[i]));
                Tools.Print("");
                Tools.Print("--Loading " + Path.GetFileName(directories[i]) + "--", "0000FF");
                string customCharacterDir = Path.Combine(CharacterDirectory, directories[i]);
                string dataFilePath = Path.Combine(customCharacterDir, DataFile);
                if (!File.Exists(dataFilePath))
                {
                    Tools.PrintError($"No \"{DataFile}\" file found for " + Path.GetFileName(directories[i]));
                    continue;
                }

                var lines = ResourceExtractor.GetLinesFromFile(dataFilePath);
                var data = ParseCharacterData(lines);

                string spritesDir = Path.Combine(customCharacterDir, "sprites");
                if (Directory.Exists(spritesDir))
                {
                    Tools.Print("Found: Sprites folder");
                    data.sprites = ResourceExtractor.GetTexturesFromDirectory(spritesDir);
                }

                string foyerDir = Path.Combine(customCharacterDir, "foyercard");
                if (Directory.Exists(foyerDir))
                {
                    Tools.Print("Found: Foyer card folder");
                    data.foyerCardSprites = ResourceExtractor.GetTexturesFromDirectory(foyerDir);
                }

                List<Texture2D> miscTextures = ResourceExtractor.GetTexturesFromDirectory(customCharacterDir);
                foreach (var tex in miscTextures)
                {
                    string name = tex.name.ToLower();
                    if (name.Equals("icon"))
                    {
                        Tools.Print("Found: Icon ");
                        data.minimapIcon = tex;
                    }
                    if (name.Equals("bosscard"))
                    {
                        Tools.Print("Found: Bosscard");
                        data.bossCard = tex;
                    }
                    if (name.Equals("playersheet"))
                    {
                        Tools.Print("Found: Playersheet");
                        data.playerSheet = tex;
                    }
                    if (name.Equals("facecard"))
                    {
                        Tools.Print("Found: Facecard");
                        data.faceCard = tex;
                    }
                }

                string punchoutDir = Path.Combine(customCharacterDir, "punchout/");

                string punchoutSpritesDir = Path.Combine(punchoutDir, "sprites");
                if (Directory.Exists(punchoutSpritesDir))
                {
                    Tools.Print("Found: Punchout Sprites folder");
                    data.punchoutSprites = ResourceExtractor.GetTexturesFromDirectory(punchoutSpritesDir);
                }

                if (Directory.Exists(punchoutDir))
                {
                    data.punchoutFaceCards = new List<Texture2D>();
                    var punchoutSprites = ResourceExtractor.GetTexturesFromDirectory(punchoutDir);
                    foreach (var tex in punchoutSprites)
                    {
                        string name = tex.name.ToLower();
                        if (name.Contains("facecard1") || name.Contains("facecard2") || name.Contains("facecard3"))
                        {
                            data.punchoutFaceCards.Add(tex);
                            Tools.Print("Found: Punchout facecard " + tex.name);
                        }
                    }
                }
                characterData.Add(data);
                Tools.StopTimerAndReport("Loading data for " + Path.GetFileName(directories[i]));
            }
        }

        private static void LoadZips()
        {
            var zipFiles = Directory.GetFiles(CharacterDirectory, "*.zip", SearchOption.TopDirectoryOnly);
            Tools.Print("# of character zip files found: " + zipFiles.Length);
            foreach (string zipFilePath in zipFiles)
            {
                string fileName = Path.GetFileName(zipFilePath);
                Tools.StartTimer("Loading data for " + fileName);
                try
                {
                    Tools.Print("");
                    Tools.Print("--Loading " + fileName + "--", "0000FF");

                    using (var zip = ZipFile.Read(zipFilePath))
                    {
                        foreach (var entry in zip)
                        {
                            if (string.Equals(Path.GetFileName(entry.FileName), DataFile, StringComparison.OrdinalIgnoreCase))
                            {
                                var ccd = ProcessCharacteryEntry(zip, entry);
                                if (ccd != null)
                                {
                                    characterData.Add(ccd);
                                    Tools.Print($"Loaded {ccd.name} from {fileName}");
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Tools.Print($"Error loading character zip: {e}");
                }
                finally
                {
                    Tools.StopTimerAndReport("Loading data for " + fileName);
                }
            }
        }

        private static CustomCharacterData ProcessCharacteryEntry(ZipFile zipFile, ZipEntry dataFileEntry)
        {
           //var lines = dataFileEntry.ReadAllLines();
            var osr = new StreamReader(dataFileEntry.OpenReader(), Encoding.Default);
            var lines = osr.ReadToEnd().Split(
                new string[] { "\r\n","\r","\n"},
                StringSplitOptions.None);
           var data = ParseCharacterData(lines);

            string customCharacterDir = Path.GetDirectoryName(dataFileEntry.FileName);
            string customCharacterDirFilter = customCharacterDir + "/";

            var directories = new Dictionary<string, List<Texture2D>>()
            {
                { customCharacterDir, null },
                { $"{customCharacterDir}/sprites", null },
                { $"{customCharacterDir}/foyercard", null },
                { $"{customCharacterDir}/punchout", null },
                { $"{customCharacterDir}/punchout/sprites", null }
            };

            foreach (var entry in zipFile)
            {
                if (!entry.FileName.StartsWith(customCharacterDirFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!entry.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    continue;

               osr = new StreamReader(entry.OpenReader(), Encoding.Default);
                var file_bytes = osr.ReadToEnd();
                byte[] textureData = File.ReadAllBytes(file_bytes);

                string fileName = Path.GetFileName(entry.FileName);
                string resourceName = fileName.Substring(0, fileName.Length - 4);
                Texture2D texture = ResourceExtractor.BytesToTexture(textureData, resourceName);

                string directoryName = Path.GetDirectoryName(entry.FileName);
                if (directories.TryGetValue(directoryName, out var list))
                {
                    if (list == null)
                    {
                        list = new List<Texture2D>();
                        directories[directoryName] = list;
                    }

                    list.Add(texture);
                }
                else
                {
                    Tools.Print($"Skipped loading {entry.FileName} in {zipFile.Name}");
                }
            }

            List<Texture2D> textures;
            if (directories.TryGetValue($"{customCharacterDir}/sprites", out textures) && textures != null)
            {
                Tools.Print("Found: Sprites folder");
                data.sprites = textures;
            }

            if (directories.TryGetValue($"{customCharacterDir}/foyercard", out textures) && textures != null)
            {
                Tools.Print("Found: Foyer card folder");
                data.foyerCardSprites = textures;
            }

            if (directories.TryGetValue(customCharacterDir, out textures) && textures != null)
            {
                foreach (var tex in textures)
                {
                    string name = tex.name.ToLower();
                    if (name.Equals("icon"))
                    {
                        Tools.Print("Found: Icon ");
                        data.minimapIcon = tex;
                    }
                    if (name.Equals("bosscard"))
                    {
                        Tools.Print("Found: Bosscard");
                        data.bossCard = tex;
                    }
                    if (name.Equals("playersheet"))
                    {
                        Tools.Print("Found: Playersheet");
                        data.playerSheet = tex;
                    }
                    if (name.Equals("facecard"))
                    {
                        Tools.Print("Found: Facecard");
                        data.faceCard = tex;
                    }
                }
            }

            if (directories.TryGetValue($"{customCharacterDir}/punchout/sprites", out textures) && textures != null)
            {
                Tools.Print("Found: Punchout Sprites folder");
                data.punchoutSprites = textures;
            }

            if (directories.TryGetValue($"{customCharacterDir}/punchout", out textures) && textures != null)
            {
                data.punchoutFaceCards = new List<Texture2D>();
                foreach (var tex in textures)
                {
                    string name = tex.name.ToLower();
                    if (name.Contains("facecard1") || name.Contains("facecard2") || name.Contains("facecard3"))
                    {
                        data.punchoutFaceCards.Add(tex);
                        Tools.Print("Found: Punchout facecard " + tex.name);
                    }
                }
            }

            return data;
        }

        //Main parse loop
        public static CustomCharacterData ParseCharacterData(string[] lines)
        {
            CustomCharacterData data = new CustomCharacterData();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].ToLower().Trim();
                string lineCaseSensitive = lines[i].Trim();

                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("#")) continue;

                if (line.StartsWith("<loadout>"))
                {
                    data.loadout = GetLoadout(lines, i + 1, out i);
                    continue;
                }

                if (line.StartsWith("<stats>"))
                {
                    data.stats = GetStats(lines, i + 1, out i);
                    continue;
                }

                int dividerIndex = line.IndexOf(':');
                if (dividerIndex < 0) continue;


                string value = lineCaseSensitive.Substring(dividerIndex + 1).Trim();
                if (line.StartsWith("base:"))
                {
                    data.baseCharacter = GetCharacterFromString(value);
                    if (data.baseCharacter == PlayableCharacters.Robot)
                        data.armor = 6;
                    continue;
                }
                if (line.StartsWith("name:"))
                {
                    data.name = value;
                    continue;
                }
                if (line.StartsWith("name short:"))
                {
                    data.nameShort = value.Replace(" ", "_");
                    data.nameInternal = "Player" + data.nameShort;
                    continue;
                }
                if (line.StartsWith("nickname:"))
                {
                    data.nickname = value;
                    continue;
                }
                if (line.StartsWith("armor:"))
                {
                    float floatValue;
                    if (!float.TryParse(value, out floatValue))
                    {
                        Tools.PrintError("Invalid armor value: " + line);
                        continue;
                    }
                    data.armor = floatValue;
                    continue;
                }
                Tools.PrintError($"Line {i} in {DataFile} did not meet any expected criteria:");
                Tools.PrintRaw("----" + line, true);
            }
            return data;
        }

        //Character name aliasing
        public static PlayableCharacters GetCharacterFromString(string characterName)
        {
            characterName = characterName.ToLower();
            foreach (PlayableCharacters character in Enum.GetValues(typeof(PlayableCharacters)))
            {
                var name = character.ToString().ToLower().Replace("coop", "");
                if (name.Equals(characterName))
                {
                    return character;
                }
            }

            if (characterName.Equals("marine"))
                return PlayableCharacters.Soldier;
            if (characterName.Equals("hunter"))
                return PlayableCharacters.Guide;
            if (characterName.Equals("paradox"))
                return PlayableCharacters.Eevee;

            Tools.Print("Failed to find character base: " + characterName);
            return PlayableCharacters.Pilot;
        }

        //Stats
        public static Dictionary<StatType, float> GetStats(string[] lines, int startIndex, out int endIndex)
        {
            endIndex = startIndex;

            Dictionary<PlayerStats.StatType, float> stats = new Dictionary<PlayerStats.StatType, float>();
            string line;
            string[] args;
            for (int i = startIndex; i < lines.Length; i++)
            {
                endIndex = i;
                line = lines[i].ToLower().Trim();
                if (line.StartsWith("</stats>")) return stats;

                args = line.Split(':');
                if (args.Length == 0) continue;
                if (string.IsNullOrEmpty(args[0])) continue;
                if (args.Length < 2)
                {
                    Tools.PrintError("Invalid stat line: " + line);
                    continue;
                }

                StatType stat = StatType.Accuracy;
                bool foundStat = false;
                foreach (StatType statType in Enum.GetValues(typeof(StatType)))
                {
                    if (statType.ToString().ToLower().Equals(args[0].ToLower()))
                    {
                        stat = statType;
                        foundStat = true;
                        break;
                    }
                }
                if (!foundStat)
                {
                    Tools.PrintError("Unable to find stat: " + line);
                    continue;
                }

                float value;
                bool foundValue = float.TryParse(args[1].Trim(), out value);
                if (!foundValue)
                {
                    Tools.PrintError("Invalid stat value: " + line);
                    continue;
                }

                stats.Add(stat, value);
            }
            Tools.PrintError("Invalid stats setup, expecting '</stats>' but found none");
            return new Dictionary<StatType, float>();
        }

        //Loadout
        public static List<Tuple<PickupObject, bool>> GetLoadout(string[] lines, int startIndex, out int endIndex)
        {
            endIndex = startIndex;

            Tools.Print("Getting loadout...");
            List<Tuple<PickupObject, bool>> items = new List<Tuple<PickupObject, bool>>();

            string line;
            string[] args;
            for (int i = startIndex; i < lines.Length; i++)
            {
                endIndex = i;
                line = lines[i].ToLower().Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("</loadout>")) return items;

                args = line.Split(' ');
                if (args.Length == 0) continue;

                if (!Gungeon.Game.Items.ContainsID(args[0]))
                {
                    Tools.PrintError("Could not find item with ID: \"" + args[0] + "\"");
                    continue;
                }
                var item = Gungeon.Game.Items[args[0]];
                if (item == null)
                {
                    Tools.PrintError("Could not find item with ID: \"" + args[0] + "\"");
                    continue;
                }

                if (args.Length > 1 && args[1].Contains("infinite"))
                {
                    var gun = item.GetComponent<Gun>();

                    if (gun != null)
                    {
                        if (!CharacterBuilder.guns.Contains(gun) && !gun.InfiniteAmmo)
                            CharacterBuilder.guns.Add(gun);

                        items.Add(new Tuple<PickupObject, bool>(item, true));
                        Tools.Print("    " + item.EncounterNameOrDisplayName + " (infinite)");
                        continue;
                    }
                    else
                    {
                        Tools.PrintError(item.EncounterNameOrDisplayName + " is not a gun, and therefore cannot be infinite");
                    }
                }
                else
                {
                    items.Add(new Tuple<PickupObject, bool>(item, false));
                    Tools.Print("    " + item.EncounterNameOrDisplayName);
                }

            }

            Tools.PrintError("Invalid loadout setup, expecting '</loadout>' but found none");
            return new List<Tuple<PickupObject, bool>>();
        }

    }
}
