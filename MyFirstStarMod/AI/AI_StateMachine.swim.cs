using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using StardewModdingAPI;
using StardewValley;

namespace MyFirstStarMod.AI
{
    internal partial class AI_StateMachine
    {
        bool swimsuit = false;
        bool swimming = false;

        /// <summary>
        /// Check if following companion switch to swim mode or back to walk
        /// </summary>
        public void CheckSwimming()
        {
            Vector2 position = this.npc.getStandingPosition();
            GameLocation location = this.npc.currentLocation;
            string action = location.doesTileHaveProperty((int)position.X / 64, (int)position.Y / 64, "TouchAction", "Back");

            if (action != null && !action.Equals("") )
            {
                this.Monitor.Log($"TouchAction: {action}", LogLevel.Debug);
                if (location.isWaterTile((int)position.X / 64, (int)position.Y / 64))
                {
                    Monitor.Log("Water tile", LogLevel.Debug);
                }
            }
            
            switch(action)
            {
                case "ChangeIntoSwimsuit":
                    this.ChangeIntoSwimsuit();
                    break;
                case "ChangeOutOfSwimsuit":
                    this.ChangeOutOfSwimsuit();
                    break;
                case "PoolEntrance":
                    this.SwitchToSwimming(location);
                    break;
                case "PoolExit":
                    this.SwitchToWalking(location);
                    break;
            }
        }

        public void SwitchToSwimming(GameLocation location)
        {
            if (this.npc.swimming.Value)
                return;

            this.npc.position.Y += 16f;
            this.npc.swimTimer = 800;
            this.npc.swimming.Value = true;
            location.playSound("pullItemFromWater");
        }

        public void SwitchToWalking(GameLocation location)
        {
            if (!this.npc.swimming.Value)
                return;

            Vector2 position = this.npc.getStandingPosition();

            this.npc.jump();
            this.npc.swimTimer = 800;
            this.npc.position.X = position.X * 64f;
            this.npc.swimming.Value = false;
            location.playSound("pullItemFromWater");
        }
        
        public void AfterWarpChangeSwimsuit()
        {
            if (this.swimsuit)
            {
                try
                {
                    this.npc.Sprite.LoadTexture($"Characters\\{this.npc.Name}_Beach");
                } 
                catch (ContentLoadException ex)
                {
                    this.Monitor.Log($"No swimsuit sprite for {this.npc.Name}: {ex.Message}", LogLevel.Debug);
                }
            }
            else
            {
                this.npc.Sprite.LoadTexture($"Characters\\{this.npc.Name}");
            }
        }

        public void ChangeIntoSwimsuit()
        {
            if (this.swimsuit)
                return;

            this.swimsuit = true;

            try
            {
                this.npc.Sprite.LoadTexture($"Characters\\{this.npc.Name}_Beach");
            } 
            catch (ContentLoadException ex)
            {
                this.Monitor.Log($"No swimsuit sprite for {this.npc.Name}: {ex.Message}", LogLevel.Debug);
            }
        }

        public void ChangeOutOfSwimsuit()
        {
            if (!this.swimsuit)
                return;

            this.swimsuit = false;
            this.npc.Sprite.LoadTexture($"Characters\\{this.npc.Name}");
        }
    }
}
