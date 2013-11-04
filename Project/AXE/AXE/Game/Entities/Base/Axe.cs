﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;

using bEngine;
using bEngine.Graphics;

using AXE.Game.Screens;
using AXE.Game.Entities;
using AXE.Game.Entities.Base;

namespace AXE.Game.Entities
{
    class Axe : Entity, IWeapon
    {
        public enum MovementState { Idle, Grabbed, Flying, Stuck, Bouncing };

        public MovementState state;

        public bSpritemap graphic;

        // When grabbed
        public IWeaponHolder holder;

        // When stuck
        public Entity stuckTo;  // Entity the axe is stuck to
        public Vector2 stuckOffset;  // Offset at (in stuckTo's coordinates) which the axe is stuck
        public Dir stuckToSide;  // StuckTo's facing direction when we got stuck
        public Dir stuckFacing;  // Axe's facing direction when we got stuck

        public float current_hspeed;
        public float current_vspeed;
        public float gravity;

        public SoundEffect sfxThrow;
        public SoundEffect sfxHit;
        public SoundEffect sfxDrop;
        public SoundEffect sfxGrab;
        public SoundEffect sfxHurt;

        public Axe(int x, int y, IWeaponHolder holder) : base(x, y)
        {
            this.holder = holder;  // can be null
        }

        public Axe(int x, int y)
            : this(x, y, null)
        {
        }

        public override void init()
        {
            base.init();

            initGraphic();
            initSoundEffects();
            initHolderState();

            initParams();
        }

        protected virtual void initParams()
        {
            mask = new bMask(0, 0, 14, 14);
            mask.game = game;
            attributes.Add("axe");
            current_hspeed = current_vspeed = 0;
            gravity = 0.5f;

            layer = 10;
        }

        protected virtual void initGraphic()
        {
            graphic = new bSpritemap(game.Content.Load<Texture2D>("Assets/Sprites/stick-sheet"), 14, 14);
            loadAnims();
        }

        protected virtual void loadAnims()
        {
            int[] fs = { 0 };
            graphic.add(new bAnim("grabbed", fs, 0.0f));
            int[] fss = { 4, 5, 6, 7 };
            graphic.add(new bAnim("cw-rotation", fss, 0.7f));
            int[] fsss = { 8, 9, 10, 11 };
            graphic.add(new bAnim("ccw-rotation", fsss, 0.7f));
            int[] fssss = { 12 };
            graphic.add(new bAnim("idle", fssss, 0.0f));
        }

        protected virtual void initHolderState()
        {
            if (holder == null)
            {
                graphic.play("idle");
                facing = Player.Dir.None;
                state = MovementState.Idle;
            }
            else
            {
                graphic.play("grabbed");
                facing = holder.getFacing();
                state = MovementState.Grabbed;
            }
        }

        protected virtual void initSoundEffects()
        {
            sfxThrow = game.Content.Load<SoundEffect>("Assets/Sfx/sfx-thrown");
            sfxHit = game.Content.Load<SoundEffect>("Assets/Sfx/axe-hit");
            sfxDrop = game.Content.Load<SoundEffect>("Assets/Sfx/axe-drop");
            sfxGrab = game.Content.Load<SoundEffect>("Assets/Sfx/sfx-grab");
            sfxHurt = game.Content.Load<SoundEffect>("Assets/Sfx/sfx-hurt");
        }

        public override int graphicWidth()
        {
            return 14;
        }

        public override void onCollision(string type, bEntity other)
        {
            if (type == "enemy" && state == MovementState.Flying)
            {
                Entity entity = other as Entity;
                onHitSolid(entity);
            }
        }

        override public void onUpdate()
        {
            base.onUpdate();

            // Prepare step
            Vector2 moveTo = pos;

            if (holder != null)
                facing = holder.getFacing();

            switch (state)
            {
                case MovementState.Grabbed:
                    pos = holder.getHandPosition() - getGrabPosition();
                    break;
                case MovementState.Flying:
                    moveTo.X += current_hspeed;
                    moveTo.Y += current_vspeed;
                    Vector2 remnant = moveToContact(moveTo, "solid");

                    // We have been stopped
                    if (remnant.X != 0 || remnant.Y != 0)
                    {
                        // Stop accelerating if we have stopped
                        bEntity entity = instancePlace(moveTo, "solid");
                        onHitSolid(entity);
                    }
                    break;
                case MovementState.Bouncing:
                    current_vspeed += gravity;

                    moveTo.Y += current_vspeed;
                    moveTo.X += current_hspeed;
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

                    if (remnant.Y != 0 && current_vspeed < 0)
                    {
                        // Touched ceiling
                        current_vspeed = 0;
                    }
                    else if (remnant.Y != 0 && current_vspeed > 0)
                    {
                        current_vspeed = 0;
                        state = MovementState.Idle;
                        sfxDrop.Play();
                    }

                    break;
                case MovementState.Stuck:
                    if (stuckTo != null)
                    {
                        if (stuckToSide == stuckTo.facing)
                        {
                            pos = stuckTo.pos + stuckOffset;
                        }
                        else
                        {
                            pos.X = stuckTo.pos.X + stuckTo.graphicWidth() - graphicWidth() - stuckOffset.X;
                            pos.Y = stuckTo.pos.Y + stuckOffset.Y;
                            facing = stuckFacing == Dir.Left ? Dir.Right : Dir.Left;
                        }
                    }

                    break;
                case MovementState.Idle:
                    bool onair = !placeMeeting(x, y + 1, "solid");
                    if (onair)
                        onair = !placeMeeting(x, y + 1, "onewaysolid", onewaysolidCondition);
                    if (onair)
                    {
                        state = MovementState.Bouncing;
                        facing = Dir.None;
                    }
                    break;
                default:
                    break;
            }
        }

        public override void onUpdateEnd()
        {
            base.onUpdateEnd();

            if (state != MovementState.Stuck)
            {
                stuckTo = null;
            }

            switch (state)
            {
                case MovementState.Stuck:
                case MovementState.Idle:
                    graphic.play("idle");
                    if (facing == Player.Dir.Left)
                        graphic.flipped = true;
                    else if (facing == Player.Dir.Right)
                        graphic.flipped = false;

                    break;
                case MovementState.Grabbed:
                    graphic.play("grabbed");
                    if (facing == Player.Dir.Left)
                        graphic.flipped = true;
                    else if (facing == Player.Dir.Right)
                        graphic.flipped = false;

                    break;
                case MovementState.Flying:
                case MovementState.Bouncing:
                    if (facing == Player.Dir.Left)
                    {
                        graphic.play("ccw-rotation");
                    }
                    else if (facing == Player.Dir.Right)
                    {
                        graphic.play("cw-rotation");
                    }
                    else
                    {
                        graphic.play("idle");
                    }

                    break;
            }

            graphic.update();
        }

        override public void render(GameTime dt, SpriteBatch sb)
        {
            base.render(dt, sb);
            graphic.render(sb, pos);
        }

        /* IWeapon implementation */
        public virtual Vector2 getGrabPosition()
        {
            return new Vector2(5, 6);
        }

        public void onGrab(IWeaponHolder holder)
        {
            sfxGrab.Play();
            holder.setWeapon(this);
            graphic.play("grabbed");
            facing = holder.getFacing();
            state = MovementState.Grabbed;
            this.holder = holder;
        }

        public virtual void onThrow(int force, Player.Dir dir)
        {
            sfxThrow.Play();
            state = MovementState.Flying;
            current_hspeed = force * holder.getDirectionAsSign(dir);
            holder.removeWeapon();
            holder = null;
        }

        public virtual void onBounce()
        {
            current_hspeed = -current_hspeed / 10;
            current_vspeed = -2;
            state = MovementState.Bouncing;
            sfxHit.Play();
        }

        public virtual void onHitSolid(bEntity entity)
        {
            // ignore entity by default
            onBounce();
        }

        public virtual void onStuck(bEntity entity)
        {
            current_hspeed = current_vspeed = 0;
            state = MovementState.Stuck;
            if (entity is Entity)
            {
                stuckTo = entity as Entity;
                stuckOffset = pos - entity.pos;
                stuckToSide = stuckTo.facing;
                stuckFacing = facing;

                sfxHurt.Play();
            }
            else
            {
                // otherwise it's a solid and the axe can remain there
                sfxHit.Play();
            }
        }
    }
}