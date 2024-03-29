﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AXE.Game.Control;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Storage;
using System.IO;
using System.Xml.Serialization;

namespace AXE.Game.Control
{
    public class GameData
    {
        public static GameData get()
        {
            return Controller.getInstance().data;
        }

        // Declare here game data
        List<string> levels;
        public int level;
        public string GetCurrentLevel
        {
            get { return levels[level]; }
        }

        public int MaxLevels 
        { 
            get 
            { 
                if (levels == null)
                    return 0;
                else
                    return levels.Count; 
            } 
        }
        public int credits;

        // Meta
        GameDataStruct state;
        public int coins
        {
            get { return state.coins; }
            set { state.coins = value; }
        }

        public PlayerData[] playerData;
        public PlayerData playerAData
        {
            get { return playerData[0]; }
        }
        public PlayerData playerBData
        {
            get { return playerData[1]; }
        }

        public GameData()
        {
            playerData = new PlayerData[] {
                new PlayerData(PlayerIndex.One),
                new PlayerData(PlayerIndex.Two)
            };
        }

        public void startNewGame()
        {
            coins = 10;
        }

        public void initPlayData()
        {
            levels = new List<string>(new string[] {
                "131201","20140111", "20140120", "20140202"
            });
            level = 0;
        }

        public static void saveGame()
        {
            IAsyncResult r = StorageDevice.BeginShowSelector(Microsoft.Xna.Framework.PlayerIndex.One, null, null);
            r.AsyncWaitHandle.WaitOne();
            StorageDevice device = StorageDevice.EndShowSelector(r);

            IAsyncResult result = device.BeginOpenContainer("AXE_DATA", null, null);
            result.AsyncWaitHandle.WaitOne();
            StorageContainer container = device.EndOpenContainer(result);
            result.AsyncWaitHandle.Close();

            string filename = "axesave.sav";
            if (container.FileExists(filename))
                container.DeleteFile(filename);
            Stream stream = container.CreateFile(filename);

            XmlSerializer serializer = new XmlSerializer(typeof(GameDataStruct));
            serializer.Serialize(stream, GameData.get().state);
            stream.Close();
            container.Dispose();
        }

        public static bool loadGame()
        {
            IAsyncResult r = StorageDevice.BeginShowSelector(Microsoft.Xna.Framework.PlayerIndex.One, null, null);
            r.AsyncWaitHandle.WaitOne();
            StorageDevice device = StorageDevice.EndShowSelector(r);

            IAsyncResult result = device.BeginOpenContainer("AXE_DATA", null, null);
            result.AsyncWaitHandle.WaitOne();
            StorageContainer container = device.EndOpenContainer(result);
            result.AsyncWaitHandle.Close();

            string filename = "axesave.sav";
            if (!container.FileExists(filename))
            {
                container.Dispose();
                return false;
            }

            Stream stream = container.OpenFile(filename, FileMode.Open);
            XmlSerializer serializer = new XmlSerializer(typeof(GameDataStruct));
            GameDataStruct tempState = (GameDataStruct)serializer.Deserialize(stream);
            stream.Close();
            container.Dispose();

            // Load Actual Coins
            GameData.get().state = tempState;

            return true;
        }
    }

    public struct GameDataStruct
    {
        public int coins;
    }
}
