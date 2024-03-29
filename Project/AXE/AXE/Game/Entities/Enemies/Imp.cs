﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

using bEngine;
using bEngine.Graphics;

using AXE.Game;
using AXE.Game.Screens;
using AXE.Game.Entities.Base;
using AXE.Game.Utils;
using AXE.Game.Entities.Axes;
using AXE.Game.Control;
using AXE.Common;
using AXE.Game.Entities.Contraptions;

namespace AXE.Game.Entities.Enemies
{
    class Imp : Enemy, IHazardProvider
    {
        public enum State { None, Idle, Turn, Walk, Chase, ChaseRunning, Attacking, Attacked, Falling, Dead }
        const int CHANGE_STATE_TIMER = 0;
        const int CHASE_REACTION_TIMER = 1;
        const int DEAD_ANIM_TIMER = 2;

        public bSpritemap spgraphic
        {
            get { return (_graphic as bSpritemap); }
            set { _graphic = value; }
        }

        // gravity things
        bool fallingToDeath;
        int deathFallThreshold;
        Vector2 fallingFrom;
        float gravity;
        float vspeed;

        Vector2 moveTo;
        bMask watchMask;
        bMaskList watchWrappedMask;

        public State state;
        bool beginChase;
        int chaseReactionTime;

        int attackThreshold;
        int attackChargeTime;
        int attackTime;
        KillerRect weaponHitZone;
        bStamp weaponHitImage;

        int hspeed;
        
        int idleBaseTime, idleOptionalTime;
        int walkBaseTime, walkOptionalTime;
        int turnBaseTime, turnOptionalTime;

        int deathAnimDuration;

        List<SoundEffect> sfxSteps;
        SoundEffect sfxHit;

        public Imp(int x, int y)
            : base(x, y)
        {
        }

        /* IReloadable implementation */
        override public void reloadContent()
        {
            spgraphic.image = (game as AxeGame).res.sprImpSheet;
            loadSoundEffects();
        }

        public override void init()
        {
            base.init();

            spgraphic = new bSpritemap((game as AxeGame).res.sprImpSheet, 30, 32);
            spgraphic.add(new bAnim("idle", new int[] { 0 }));
            spgraphic.add(new bAnim("turn", new int[] { 9 }));
            spgraphic.add(new bAnim("walk", new int[] { 1, 2, 3, 2 }, 0.3f));
            spgraphic.add(new bAnim("chase-reacting", new int[] { 4 }));
            spgraphic.add(new bAnim("chase", new int[] { 1, 2, 3, 2 }, 0.5f));
            spgraphic.add(new bAnim("chase-running-reacting", new int[] { 10 }));
            spgraphic.add(new bAnim("chase-running", new int[] { 11, 12 }, 0.5f));
            spgraphic.add(new bAnim("attack-charge", new int[] { 16, 17, 17, 17 }, 0.4f, false));
            spgraphic.add(new bAnim("attacked", new int[] { 18 }));
            spgraphic.add(new bAnim("jump", new int[] { 8 }));
            spgraphic.add(new bAnim("death", new int[] { 24 }));
            spgraphic.play("idle");

            mask.w = 16;
            mask.h = 21;
            mask.offsetx = 7;
            mask.offsety = 11;

            watchMask = new bMask(x, y, 90, 24);
            bMask maskL = new bMask(0, 0, 0, 0);
            maskL.game = game;
            bMask maskR = new bMask(0, 0, 0, 0);
            maskR.game = game;
            watchWrappedMask = new bMaskList(new bMask[] { maskL, maskR }, 0, 0, false);
            watchWrappedMask.game = game;

            hspeed = 1;
            vspeed = 0f;
            gravity = 0.5f;
            deathFallThreshold = 40;

            idleBaseTime = 80;
            idleOptionalTime = 80;
            walkBaseTime = 30;
            walkOptionalTime = 30;
            turnBaseTime = 60;
            turnOptionalTime = 60;
            deathAnimDuration = 50;

            if (Tools.random.Next(2) < 1)
                facing = Dir.Right;
            else
                facing = Dir.Left;

            // tamed = false;
            beginChase = false;
            chaseReactionTime = 15;

            attackThreshold = 30;
            attackChargeTime = 10;
            attackTime = 8;
            weaponHitImage = new bStamp(spgraphic.image, new Rectangle(90, 64, 30, 32));

            loadSoundEffects();

            state = State.None;
            changeState(State.Idle);
            
            attributes.Add(ATTR_SOLID);
        }

        public void loadSoundEffects()
        {
            sfxSteps = new List<SoundEffect>();
            sfxSteps.Add((game as AxeGame).res.sfxDirtstepA);
            sfxSteps.Add((game as AxeGame).res.sfxDirtstepB);
            sfxSteps.Add((game as AxeGame).res.sfxDirtstepC);
            sfxHit = (game as AxeGame).res.sfxPlayerHit;
        }

        public void changeState(State newState)
        {
            if (newState != state)
            {
                bool performChange = true;
                switch (newState)
                {
                    case State.Idle:
                        timer[0] = idleBaseTime + Tools.random.Next(idleOptionalTime) - idleOptionalTime;
                        break;
                    case State.Walk:
                        timer[0] = walkBaseTime + Tools.random.Next(walkOptionalTime) - walkOptionalTime;
                        break;
                    case State.Turn:
                        timer[0] = turnBaseTime + Tools.random.Next(turnOptionalTime) - turnOptionalTime;
                        break;    
                    case State.Chase:
                        /*if (tamed)
                        {
                            performChange = false;
                            changeState(State.Idle);
                        }*/
                        beginChase = false;
                        timer[1] = chaseReactionTime;
                        break;
                    case State.ChaseRunning:
                        /*if (tamed)
                        {
                            performChange = false;
                            changeState(State.Idle);
                        }*/
                        beginChase = false;
                        timer[1] = (int)(chaseReactionTime * 1.5f);
                        break;
                    case State.Attacking:
                        /*if (tamed)
                        {
                            performChange = false;
                            changeState(State.Idle);
                        }*/
                        timer[0] = attackChargeTime;
                        break;
                    case State.Attacked:
                        /*if (tamed)
                        {
                            performChange = false;
                            changeState(State.Idle);
                        }*/

                        int xx, yy = 4;
                        if (facing == Dir.Right)
                            xx = 20;
                        else
                            xx = -10;
                        weaponHitZone = new KillerRect(x + xx, y + yy, 20, 27, Player.DeathState.ForceHit);
                        weaponHitZone.setOwner(this);
                        world.add(weaponHitZone, "hazard");
                        timer[0] = attackTime;
                        break;
                }

                if (performChange)
                    state = newState;
            }
        }

        public override void onTimer(int n)
        {
            switch (n)
            {
                case CHANGE_STATE_TIMER:
                    switch (state)
                    {
                        case State.Idle:
                            if (Tools.random.Next(2) < 1)
                                changeState(State.Turn);
                            else
                                changeState(State.Walk);
                            break;
                        case State.Walk:
                            if (Tools.random.Next(2) < 1)
                                changeState(State.Turn);
                            else
                                changeState(State.Idle);
                            break;
                        case State.Turn:
                            if (facing == Dir.Left)
                                facing = Dir.Right;
                            else
                                facing = Dir.Left;

                            changeState(State.Walk);
                            break;
                        case State.Chase:
                        case State.ChaseRunning:
                            break;
                        case State.Attacking:
                            changeState(State.Attacked);
                            // Sound!
                            break;
                        case State.Attacked:
                            changeState(State.Idle);
                            if (weaponHitZone != null)
                            {
                                world.remove(weaponHitZone);
                                weaponHitZone = null;
                            }

                            break;
                    }
                    break;
                case CHASE_REACTION_TIMER:
                    if (state == State.Chase || state == State.ChaseRunning)
                        beginChase = true;
                    break;
                case DEAD_ANIM_TIMER:
                    break;
            }
        }

        public override void onUpdate()
        {
            base.onUpdate();

            spgraphic.update();

            moveTo = pos;
            bool onAir = !checkForGround(x, y);

            if (onAir)
            {
                state = State.Falling;
                fallingFrom = pos;
                fallingToDeath = false;
            }

            switch (state)
            {
                case State.Idle:
                    spgraphic.play("idle");
                    break;
                case State.Walk:
                    spgraphic.play("walk");

                    Vector2 nextPosition = new Vector2(x + directionToSign(facing) * hspeed, y);
                    bool wontFall = checkForGround(
                            (int)(nextPosition.X + directionToSign(facing) * graphicWidth() / 2),
                            (int)nextPosition.Y);
                    bool wontCollide = !placeMeeting(
                            (int)nextPosition.X,
                            (int)nextPosition.Y, new String[] { "player", "solid" });
                    if (wontFall && wontCollide)
                        moveTo.X += directionToSign(facing) * hspeed;
                    else if (!wontFall)
                        changeState(State.Idle);
                    else if (!wontCollide)
                        changeState(State.Turn);

                    break;
                case State.Turn:
                    spgraphic.play("turn");
                    break;
                case State.Falling:
                    if (onAir)
                    {
                        vspeed += gravity;
                        if (vspeed > 0 && fallingFrom == Vector2.Zero)
                        {
                            fallingToDeath = false;
                            fallingFrom = pos;
                        }

                        if (vspeed > 0 && pos.Y - fallingFrom.Y >= deathFallThreshold)
                        {
                            fallingToDeath = true;
                        }
                    }
                    else
                    {
                        if (fallingToDeath)
                            onDeath(null); // You'd be dead, buddy!
                        changeState(State.Idle);
                    }

                    moveTo.Y += vspeed;

                    spgraphic.play("jump");

                    break;
                case State.Chase:
                case State.ChaseRunning:
                    if (beginChase)
                    {
                        if (state == State.Chase)
                            spgraphic.play("chase");
                        else
                            spgraphic.play("chase-running");

                        // Should I keep trying?
                        bool shouldKeepChasing = true;
                        if (Tools.random.Next(30) < 1)
                        {
                            if (!isPlayerOnSight(facing, false, new String[] { "solid" }, watchMask, watchWrappedMask))
                            {

                                shouldKeepChasing = false;
                            }
                        }

                        if (shouldKeepChasing)
                        {
                            int hsp = (int)(hspeed * 2 * (state == State.ChaseRunning ? 1.5 : 1));
                            nextPosition = new Vector2(x + directionToSign(facing) * hsp, y);
                            wontFall = checkForGround(
                                    (int)(nextPosition.X + directionToSign(facing) * graphicWidth() / 2),
                                    (int)nextPosition.Y);
                            wontCollide = !placeMeeting(
                                    (int)nextPosition.X,
                                    (int)nextPosition.Y, new String[] { "player", "solid" });
                            if (wontFall && wontCollide)
                                moveTo.X += directionToSign(facing) * hsp;
                            else if (!wontFall || !wontCollide)
                                changeState(State.Idle);
                        }
                        else
                        {
                            changeState(State.Idle);
                        }
                    }
                    else
                    {
                        if (state == State.Chase)
                            spgraphic.play("chase-reacting");
                        else
                            spgraphic.play("chase-running-reacting");
                    }
                    break;
                case State.Attacking:
                    spgraphic.play("attack-charge");
                    break;
                case State.Attacked:
                    spgraphic.play("attacked");
                    break;
                case State.Dead:
                    spgraphic.play("death");
                    float factor = (timer[DEAD_ANIM_TIMER] / (deathAnimDuration * 1f));
                    color *= factor;
                    if (color.A <= 0)
                    {
                        world.remove(this);
                    }
                    break;
            }

            if (state == State.Idle || state == State.Walk || state == State.Turn)
            {
                Dir facingDir = facing;
                if (state == State.Turn)
                    if (facingDir == Dir.Left) facingDir = Dir.Right;
                    else facingDir = Dir.Left;
                if (facingDir == Dir.Left)
                    watchMask.offsetx = _mask.offsetx -watchMask.w;
                else
                    watchMask.offsetx = _mask.offsetx + _mask.w;
                watchMask.offsety = (graphicHeight() - watchMask.h);

                if (isPlayerOnSight(facingDir, false, new String[] { "solid" }, watchMask, watchWrappedMask))
                {
                    facing = facingDir;
                    changeState(State.Chase);
                }
            }
            else if (state == State.Chase)
            {
                Player[] players = (world as LevelScreen).players;
                foreach (Player player in players)
                {
                    if (player != null && player.state != Player.MovementState.Death && (player.pos - pos).Length() < attackThreshold)
                    {
                        changeState(State.Attacking);
                    }
                }
            }

            if (state == State.Walk || state == State.Chase || state == State.ChaseRunning || state == State.Falling)
            {
                Vector2 remnant;
                // Check wether we collide first with a solid or a onewaysolid,
                // and use that data to position the player character.
                Vector2 oldPos = pos;
                Vector2 remnantOneWay = moveToContact(moveTo, "onewaysolid", onewaysolidCondition);
                Vector2 posOneWay = pos;
                pos = oldPos;
                Vector2 remnantSolid = moveToContact(moveTo, "solid");
                Vector2 posSolid = pos;
                if (remnantOneWay.Length() > remnantSolid.Length())
                {
                    remnant = remnantOneWay;
                    pos = posOneWay;
                }
                else
                {
                    remnant = remnantSolid;
                    pos = posSolid;
                }

                // We have been stopped
                if (remnant.X != 0)
                {
                }

                // The y movement was stopped
                if (remnant.Y != 0 && vspeed < 0)
                {
                    // Touched ceiling
                    vspeed = 0;
                }
                else if (remnant.Y != 0 && vspeed > 0)
                {
                    // Landed
                    /*isLanding = true;
                    sfxSteps[0].Play();
                    sfxSteps[1].Play();*/
                }
            }

            spgraphic.flipped = (facing == Dir.Left);

            handleSoundEffects();

            // Uberdebuggo temporal thingie!
            if (mouseHover && input.check(Microsoft.Xna.Framework.Input.Keys.D))
                world.remove(this);
        }

        public override void render(GameTime dt, SpriteBatch sb)
        {
            base.render(dt, sb);
            spgraphic.color = color;
            spgraphic.render(sb, pos);
            if (state == State.Attacked)
                if (facing == Dir.Left)
                {
                    weaponHitImage.flipped = true;
                    weaponHitImage.render(sb, new Vector2(x - weaponHitImage.width, y));
                }
                else
                {
                    weaponHitImage.flipped = false;
                    weaponHitImage.render(sb, new Vector2(x + graphicWidth(), y));
                }

            if (bConfig.DEBUG)
                sb.DrawString(game.gameFont, state.ToString() + " [" + timer[0] + "]", new Vector2(x, y - 8), Color.White);
        }

        public override int graphicWidth()
        {
            return spgraphic.width;
        }

        public override int graphicHeight()
        {
            return spgraphic.height;
        }

        bool playedStepEffect = false;
        public void handleSoundEffects()
        {
            float relativeX = pos.X / (world as LevelScreen).width - 0.5f;
            switch (state)
            {
                case State.Walk:
                case State.Chase:
                case State.ChaseRunning:
                    int currentFrame = spgraphic.currentAnim.frame;
                    if (currentFrame == 2 && !playedStepEffect)
                    {
                        playedStepEffect = true;
                        sfxSteps[Utils.Tools.random.Next(sfxSteps.Count)].Play(0.5f, 0.0f, relativeX);
                    }
                    else if (currentFrame != 2)
                        playedStepEffect = false;

                    break;
                default:
                    break;
            }
        }

        public override void onDeath(Entity killer)
        {
            if (state != State.Dead)
            {
                sfxHit.Play();
                state = State.Dead;
                color = new Color(164, 0, 0, 255);
                timer[DEAD_ANIM_TIMER] = deathAnimDuration;
            }

            base.onDeath(killer);
        }

        public override bool onHit(Entity other)
        {
            base.onHit(other);

            if (other is NormalAxe)
            {
                Entity killer = other.getKillOwner();
                onDeath(killer);

                if (rewarder != null)
                {
                    if (contraptionRewardData.target == null)
                    {
                        contraptionRewardData.target = ((other as NormalAxe).thrower as bEntity);
                    }
                }
                onSolved();

                return true;
            }
            else if (other is Axe)
            {
                // Get MaD!
                if (other.x + other.graphicWidth() / 2 < x + graphicWidth() / 2)
                    facing = Dir.Left;
                else facing = Dir.Right;
                changeState(State.ChaseRunning);

                return false;
            }

            return false;
        }

        /**
         * IHAZARDPROVIDER METHODS
         */
        public void onSuccessfulHit(Player other)
        {
            // tamed = true;
        }
    }
}
