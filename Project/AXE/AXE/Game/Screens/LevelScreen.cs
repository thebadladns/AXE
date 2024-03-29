﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using bEngine;
using bEngine.Graphics;
using bEngine.Helpers;

using AXE.Common;
using AXE.Game.Entities;
using AXE.Game.Control;
using AXE.Game.Entities.Axes;
using AXE.Game.Entities.Contraptions;
using AXE.Game.Entities.Base;
using AXE.Game.Utils;
using AXE.Game.UI;
using Microsoft.Xna.Framework.Media;
using AXE.Game.Entities.Decoration;

namespace AXE.Game.Screens
{
    class LevelScreen : Screen
    {
        // Some declarations...
        public enum State { Enter, Gameplay, Exit };

        // Render ordering
        List<bEntity> renderQueue;

        // Management
        public string id;
        public String name;
        public int timeLimit;
        public int timeCount;
        bool paused;

        public State state;

        // Level elements
        public int width { get { return levelMap.tilemap.width; } }
        public int height { get { return levelMap.tilemap.height; } }
        public bCamera2d camera;
        LevelMap levelMap;
        protected bStamp background;

        // Players
        public Player playerA, playerB;
        public Player[] players { get { return new Player[] {playerA, playerB}; } }
        // Exit
        public int playersThatLeft;

        // Screen text
        public string timeLabelText;
        public IntermittentLabel timeLabel;

        public string stageLabel;
        public string infoLabel;
        PlayerDisplay[] playerDisplays;

        public bool shouldPlayMusicOnInit = true;
        Song bgMusic;

        // Debug
        // String msg;
        public bStamp cursor;
        
        public LevelScreen(string id, int lastCheckpoint = -1)
            : base()
        {
            this.id = id;
            usesCamera = true;
        }

        /* IReloadable implementation */
        override public void reloadContent()
        {
            cursor.image = (game as AxeGame).res.sprCursor;
            timeLabel.sound = (game as AxeGame).res.sfxMidBell;
            bgMusic = (game as AxeGame).res.getSong(levelMap.bgMusicName);
        }

        public override void init()
        {
            base.init();

            paused = false;

            cursor = new bStamp((game as AxeGame).res.sprCursor);
            state = State.Enter;

            // Init entity collections
            entities.Add("solid", new List<bEntity>());
            entities.Add("onewaysolid", new List<bEntity>());
            entities.Add("items", new List<bEntity>());
            entities.Add("player", new List<bEntity>());
            entities.Add("axe", new List<bEntity>());
            entities.Add("hazard", new List<bEntity>());
            entities.Add("enemy", new List<bEntity>());
            entities.Add("stairs", new List<bEntity>());
            entities.Add("contraptions", new List<bEntity>());
            entities.Add("rewarders", new List<bEntity>());
            entities.Add("decoration", new List<bEntity>());

            // Load level
            String fname = id.ToString();
            levelMap = new LevelMap(fname);
            _add(levelMap, "solid"); // Adding to world performs init & loading
            name = levelMap.name;
            timeLimit = levelMap.timeLimit * (game as AxeGame).FramesPerSecond;
            timeCount = timeLimit;

            // Load background
            background = null;

            // Add players
            foreach (PlayerData pdata in GameData.get().playerData)
            {
                spawnPlayer(pdata);
            }

            // Add loaded entities
            handleEntities(levelMap.entities);

            // Start
            camera = new bCamera2d(game.GraphicsDevice);
            camera.bounds = new Rectangle(levelMap.x, levelMap.y, levelMap.tilemap.width, levelMap.tilemap.height);
   
            state = State.Gameplay;

            renderQueue = new List<bEntity>();

            playerDisplays = new PlayerDisplay[] { 
                new PlayerDisplay(PlayerIndex.One, GameData.get().playerData[0], playerA),
                new PlayerDisplay(PlayerIndex.Two, GameData.get().playerData[1], playerB)
            };

            for (int i = 0; i < playerDisplays.Length; i++)
            {
                playerDisplays[i].world = this;
                playerDisplays[i].game = game;
                playerDisplays[i].init();
            }

            playersThatLeft = 0;

            timeLabel = new IntermittentLabel(0, 10, "", Color.White, false, 15, (game as AxeGame).res.sfxMidBell);
            timeLabel.game = game;
            timeLabel.mask.game = game;

            bgMusic = (game as AxeGame).res.getSong(levelMap.bgMusicName);

            if (shouldPlayMusicOnInit)
            {
                playBGMusic();
            }
            else
            {
                MediaPlayer.Stop();
            }
        }

        public void playBGMusic()
        {
            /*if (MediaPlayer.Queue.ActiveSong != bgMusic)
            {
                MediaPlayer.Play(bgMusic);
                MediaPlayer.IsRepeating = true;
            }*/
        }

        public Player spawnPlayer(PlayerData pdata)
        {
            Player player = null;
            // TODO: add logic for positioning and multiplayer
            if (pdata.playing && pdata.alive)
            {
                int playerX = (int)levelMap.playerStart.X;
                int playerY = (int)levelMap.playerStart.Y;

                if (pdata.id == PlayerIndex.One)
                {
                    // Removing the old player prevents glitches
                    // but also removes the corpse and is less funny
                    if (playerA != null)
                        remove(playerA);
                    player = new Player(playerX, playerY, GameData.get().playerData[0]);
                    playerA = player;
                    // Adding axe based on GameData
                    spawnPlayerWeapon(playerA.data, playerA);
                    _add(playerA, "player");
                }
                else if (pdata.id == PlayerIndex.Two)
                {
                    // Removing the old player prevents glitches
                    // but also removes the corpse and is less funny
                    if (playerB != null)
                        remove(playerB);
                    player = new Player(playerX + 32, playerY, GameData.get().playerData[1]);
                    playerB = player;
                    // Adding axe based on GameData
                    spawnPlayerWeapon(playerB.data, playerB);
                    _add(playerB, "player");
                }

                int ntries = 0;
                while (player.placeMeeting(player.x, player.y, new String[] { "player", "enemy", "solid" }))
                {
                    // Pferv-like fail safe
                    ntries++;
                    if (ntries > width)
                        break;

                    player.x++;
                }
            }

            return player;
        }

        public void resetTimeLimit()
        {
            timeCount = timeLimit * (game as AxeGame).FramesPerSecond;
        }

        public void boostTtimeLimit(int howManySeconds)
        {
            timeCount += howManySeconds * (game as AxeGame).FramesPerSecond;
        }

        public override void update(GameTime dt)
        {
            base.update(dt);

            foreach (String key in entities.Keys)
                foreach (bEntity entity in entities[key])
                    entity.update();

            // Collisions
            if (!paused && state == State.Gameplay)
            {
                foreach (bEntity p in entities["player"])
                {
                    foreach (bEntity pp in entities["player"])
                        if (p != pp && p.collides(pp))
                        {
                            p.onCollision("player", pp);
                            pp.onCollision("player", p);
                        }
                    foreach (bEntity e in entities["enemy"])
                        if (p != e && p.collides(e))
                        {
                            e.onCollision("player", p);
                            p.onCollision("enemy", e);
                        }
                    foreach (bEntity i in entities["items"])
                        if (p != i && p.collides(i))
                        {
                            p.onCollision("items", i);
                            i.onCollision("player", p);
                        }
                    foreach (bEntity h in entities["hazard"])
                        if (p != h && p.collides(h))
                        {
                            h.onCollision("player", p);
                            p.onCollision("hazard", h);
                        }
                    foreach (bEntity c in entities["items"])
                        if (p != c && p.collides(c))
                        {
                            c.onCollision("player", p);
                            p.onCollision("items", c);
                        }
                    foreach (bEntity a in entities["axe"])
                        if (p != a && p.collides(a))
                        {
                            a.onCollision("player", p);
                            p.onCollision("axe", a);
                        }
                }

                foreach (bEntity e in entities["enemy"])
                {
                    foreach (bEntity w in entities["hazard"])
                        if (e.collides(w))
                        {
                            e.onCollision("hazard", w);
                            w.onCollision("enemy", e);
                        }
                }

                foreach (bEntity w in entities["axe"])
                {
                    foreach (bEntity e in entities["enemy"])
                        if (w.collides(e))
                        {
                            e.onCollision("axe", w);
                            w.onCollision("enemy", e);
                        }

                    foreach (bEntity e in entities["axe"])
                        if (w != e && w.collides(e))
                        {
                            // Check first, since it depends on the state of the axe and the
                            // first onCollision will set the other axe to bounce, hence it won't
                            // be flying on the next on collision and things won't work
                            if (axeToAxeCollisionCondition(w as Axe, e as Axe)) 
                            {
                                e.onCollision("axe", w);
                                w.onCollision("axe", e);
                            }
                        }
                }
                    
            }
                        
            // Debug: R for restart
            if (bGame.input.pressed(Microsoft.Xna.Framework.Input.Keys.R))
                game.changeWorld(new LevelScreen(id));
            if (bGame.input.pressed(Microsoft.Xna.Framework.Input.Keys.N))
                Controller.getInstance().goToNextLevel();
            if (bGame.input.pressed(Microsoft.Xna.Framework.Input.Keys.M))
                Controller.getInstance().goToPreviousLevel();

            for (int i = 0; i < playerDisplays.Length; i++)
                playerDisplays[i].update();

            timeLabelText = String.Format("{0}", timeCount / (game as AxeGame).FramesPerSecond);
            timeLabel.x = game.getWidth() / 2 - timeLabelText.Length * 8 / 2;
            timeLabel.label = timeLabelText;
            timeLabel.update();

            stageLabel = buildStageLabel();
            infoLabel = "CREDITS: " + (GameData.get().credits) + " - COINS: " + (GameData.get().coins + " ( " + Controller.getInstance().activePlayers + ")");

            // Time limit!!
            if (!paused)
            {
                timeCount--;
                if ((timeCount / (float)(game as AxeGame).FramesPerSecond) <= 11 && !timeLabel.intermittent)
                {
                    timeLabel.intermittent = true;
                }

                if (timeCount == 0)
                {
                    Controller.getInstance().onGameOver();
                }
            }
        }

        public string buildStageLabel()
        {
            string number = "" + (id + 1);
            while (number.Length < 2)
                number = " " + number;
            string label = "STAGE " + number;
            return label;
        }

        public override void render(GameTime dt, SpriteBatch sb, Matrix matrix)
        {
            base.render(dt, sb, matrix);

            //matrix *= camera.get_transformation();

            /*sb.End();

            sb.Begin(SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    SamplerState.PointClamp,
                    null,
                    RasterizerState.CullCounterClockwise,
                    (game as AxeGame).effect,
                    matrix);*/

            // Render bg
            sb.Draw(bDummyRect.sharedDummyRect(game), game.getViewRectangle(), Color.Black);
            if (background != null)
                background.render(sb, Vector2.Zero);

            // Render entities
            renderQueue.Clear();
            foreach (String key in entities.Keys)
                foreach (bEntity entity in entities[key])
                    if (!(entity is Entity) || (entity is Entity) && ((entity as Entity).visible))
                        renderQueue.Add(entity);

            renderQueue.Sort((a, b) => (b.layer - a.layer));
            foreach (bEntity entity in renderQueue)
                entity.render(dt, sb);

            cursor.render(sb, bGame.input.mousePosition);

            for (int i = 0; i < playerDisplays.Length; i++)
                playerDisplays[i].render(dt, sb);

            timeLabel.render(dt, sb);
            sb.DrawString(game.gameFont, stageLabel, new Vector2(game.getWidth() / 2 - stageLabel.Length * 8 / 2, 0), Color.White);
            sb.DrawString(game.gameFont, infoLabel, new Vector2(game.getWidth()/2-infoLabel.Length*8/2, game.getHeight()-8), Color.White);

            // Pause!
            if (paused)
            {
                // Pause render
                sb.DrawString(game.gameFont, "PAUSE", new Vector2(game.getWidth() / 2 - ("PAUSE".Length * 8) / 2), Color.White);
            }
        }

        protected override bool _add(bEntity e, string category)
        {
            switch (category)
            {
                case "player":
                    entities["player"].Add(e);
                    Player player = (e as Player);
                    if (player.data.id == 0)
                        playerA = player;
                    else
                        playerB = player;
                    break;
                case "enemy":
                    entities["enemy"].Add(e);
                    break;
                case "solid":
                    entities["solid"].Add(e);
                    break;
                default:
                    if (entities.ContainsKey(category))
                    {
                        entities[category].Add(e);
                        return base._add(e, category);
                    }
                    else
                        return false;
            }

            return base._add(e, category);
        }

        public bool isPaused()
        {
            return paused;
        }

        public void handlePause()
        {
            paused = !paused;
        }

        public void handleEntities(List<bEntity> list)
        {
            foreach (bEntity e in list)
            {
                if (e == null)
                    continue;
                else if (e is OneWayPlatform)
                    _add(e, "onewaysolid");
                else if (e is Stairs)
                    _add(e, "stairs");
                else if (e is ExitDoor)
                    _add(e, "items");
                else if (e is TreasureChest)
                    _add(e, "items");
                else if (e is Enemy)
                    _add(e, "enemy");
                else if (e is Axe)
                    _add(e, "axe");
                else if (e is Item)
                    _add(e, "items");
                else if (e is TrapDoor)
                    _add(e, "onewaysolid");
                else if (e is Lever)
                    _add(e, "contraptions");
                else if (e is MoveablePlatform)
                    _add(e, "onewaysolid");
                else if (e is Door)
                    _add(e, "solid");
                else if (e is Key)
                    _add(e, "items");
                // Contraptions and rewarders may be added before, but if they are not,
                // we'll add them to these categories
                else if (e is IContraption)
                    _add(e, "contraptions");
                else if (e is IRewarder)
                    _add(e, "rewarders");
                else if (e is Decoration)
                    _add(e, "decoration");
            }
        }

        public override bool isInstanceInView(bEntity e)
        {
            Rectangle viewRect = camera.viewRectangle;
            viewRect.Inflate(viewRect.Width / 4, viewRect.Height / 4);

            return viewRect.Intersects(e.mask.rect);
        }

        public int layerSelector(bEntity a, bEntity b)
        {
            return a.layer - b.layer;
        }

        public bool axeToAxeCollisionCondition(Axe a, Axe b)
        {
            return a.state == Axe.MovementState.Flying && b.state == Axe.MovementState.Flying;
        }

        public void displayPlayerCountdown(PlayerIndex who)
        {
            if (who == PlayerIndex.One)
            {
                playerDisplays[0].startTimer();
            }
            else
                playerDisplays[1].startTimer();
        }

        public void spawnPlayerWeapon(PlayerData data, Player player)
        {
            Axe currentWeapon = null;
            switch (data.weapon)
            {
                case PlayerData.Weapons.None:
                    break;
                case PlayerData.Weapons.Stick:
                    currentWeapon = new Axe(player.x, player.y, player);
                    break;
                case PlayerData.Weapons.Axe:
                    currentWeapon = new NormalAxe(player.x, player.y, player);
                    break;
            }
            if (currentWeapon != null)
            {
                player.setWeapon(currentWeapon);
                _add(currentWeapon, "axe");
            }
        }
    }
}
