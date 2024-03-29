﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using bEngine;
using bEngine.Helpers;
using bEngine.Helpers.Transitions;

using AXE.Common;
using AXE.Game;
using AXE.Game.Screens;
using Microsoft.Xna.Framework;
using AXE.Game.Utils;
using Microsoft.Xna.Framework.Media;
using AXE.Game.Entities;

namespace AXE.Game.Control
{
    class Controller
    {
        public bool testLaunch = true;

        static Controller _instance;
        public static Controller getInstance()
        {
            if (_instance == null)
                _instance = new Controller();
            return _instance;
        }

        AxeGame game;
        public GameData data;

        public int activePlayers;
        public GameInput[] playerInput;
        public GameInput playerAInput
        {
            get { return playerInput[0]; }
        }
        public GameInput playerBInput
        {
            get { return playerInput[1]; }
        }

        Controller()
        {
            data = new GameData();
            playerInput = new GameInput[] {
                new GameInput(PlayerIndex.One),
                new GameInput(PlayerIndex.Two)
            };
        }

        public void setGame(AxeGame game)
        {
            this.game = game;
        }

        public void onLogoStart()
        {
            game.changeWorld(new LogoScreen(), new FadeToColor(game, Color.Black));
        }

        public void onMenuStart()
        {
            // init game data here
            if (!GameData.loadGame())
                GameData.get().startNewGame();
            game.changeWorld(new TitleScreen(), new FadeToColor(game, Color.Black));
        }

        public void changePlayerButtonConf(PlayerIndex index, Dictionary<PadButton, List<Object>> mappingConf)
        {
            GameInput.getInstance(index).setMapping(mappingConf);
            // store to disk maybe?
        }

        public void onGameStart()
        {
            GameData data = GameData.get();
            // Init data
            data.initPlayData();
            
            // Set alive the playing characters
            if (data.playerAData.playing)
            {
                data.playerAData.alive = true;
            }
            if (data.playerBData.playing)
            {
                data.playerBData.alive = true;
            }

            // Go to first screen
            game.changeWorld(new LevelScreen(data.GetCurrentLevel), new FadeToColor(game, Color.Black, 10));
        }

        public void onGameEnd()
        {
        }

        public void onGameOver()
        {
            MediaPlayer.Play(ResourceManager.get().ostGameOver);
            game.changeWorld(new GameOverScreen(), new FadeToColor(game, Color.Black, 40/*120*/));
        }

        public void onGameWin()
        {
            game.changeWorld(new WinScreen(), new FadeToColor(game, Color.Gray, 15));
        }

        public int goToNextLevel()
        {
            GameData.saveGame();

            // Handle level progression
            data.level += 1;
            if (data.level >= data.MaxLevels)
                onGameWin();
            else
                game.changeWorld(new LevelScreen(data.GetCurrentLevel), new FadeToColor(game, Colors.clear, 10));
            return data.level;
        }

        public int goToPreviousLevel()
        {
            GameData.saveGame();

            // Handle level progression
            data.level -= 1;
            if (data.level < 0)
                data.level += data.MaxLevels;
            game.changeWorld(new LevelScreen(data.GetCurrentLevel), new FadeToColor(game, Colors.clear, 10));

            return data.level;
        }

        public void handlePlayerDeath(PlayerData who)
        {
            who.alive = false;
            (game.world as LevelScreen).displayPlayerCountdown(who.id);
        }

        public void handleCountdownEnd(PlayerIndex who)
        {
            if (who == PlayerIndex.One)
                data.playerAData.playing = false;
            else if (who == PlayerIndex.Two)
                data.playerBData.playing = false;

            activePlayers--;
            if (activePlayers <= 0)
                onGameOver();
        }

        /** Returns true if valid start press **/
        public bool playerStart(PlayerIndex who)
        {
            if (GameData.get().credits > 0)
            {
                GameData.get().credits--;
                PlayerData pdata;
                if (who == PlayerIndex.One)
                    pdata = GameData.get().playerAData;
                else if (who == PlayerIndex.Two)
                    pdata = GameData.get().playerBData;
                else
                    return false;

                if (!pdata.playing)
                {
                    // Give new axe on game start
                    // NO!! >:(
                    //if (pdata.weapon == PlayerData.Weapons.None)
                    //    pdata.weapon = PlayerData.Weapons.Axe;

                    pdata.playing = true;
                    activePlayers++;
                }
                pdata.alive = true;
                
                return true;
            }

            return false;
        }

        public bool canSwitchFullscreen()
        {
            return true;
        }

        public void launch()
        {
            if (testLaunch)
            {
                onMenuStart();
            }
            else
            {
                onLogoStart();
            }
        }

        public void applyScore(Entity receiver, Object scoreable)
        {
            // Only reward players if we know one of them killed them,
            // or if we don't know anything
            if (receiver == null || receiver is Player)
            {
                int score = ScoreManager.getScore(scoreable);

                // If we don't know who killed it, reward both
                if (receiver == null)
                {
                    if (GameData.get().playerAData.playing)
                        GameData.get().playerAData.score += score;
                    if (GameData.get().playerBData.playing)
                        GameData.get().playerBData.score += score;
                }
                // If we do, reward both
                else
                {
                    (receiver as Player).data.score += score;
                }
            }
        }    
    }
}
