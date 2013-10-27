using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

using bEngine;
using AXE.Game;

namespace AXE
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class AxeGame : bGame
    {
        Screen screen;

        protected override void initSettings()
        {
            base.initSettings();

            horizontalZoom = 3;
            verticalZoom = 3;

            width = 320;
            height = 256;
        }

        protected override void Initialize()
        {
            screen = new LogoScreen();
            changeWorld(screen);

            base.Initialize();
        }

        public override void update(GameTime gameTime)
        {
            Common.GameInput.getInstance().update();

            base.update(gameTime);
        }
    }
}
