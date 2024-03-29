﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using AXE.Game.Utils;

namespace AXE.Game.Control
{
    /**
     * Stores persistent info of the player (powerups, health)
     */
    public class PlayerData
    {
        // Defs
        public enum Weapons { None, Stick, Axe, Small };
        public const int KEY_YELLOW  = 1;
        public const int KEY_RED     = 2;
        public const int KEY_BLUE    = 3;

        // Playstate
        public PlayerIndex id;
        public bool playing;
        public bool alive;

        // Status
        public Weapons weapon;
        public int powerUps;
        public int keys;

        // Session achievements
        public int collectedCoins;
        public int kills;
        public int treausures;
        public int score;
        public int souls;

        public PlayerData(PlayerIndex id)
        {
            this.id = id;
            playing = false;
            alive = false;

            weapon = Weapons.Axe;
            powerUps = 0;
            keys = 0;

            collectedCoins = 0;
            kills = 0;
            treausures = 0;
            score = 0;
            souls = 0;
        }
    }
}
