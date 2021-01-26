/*
MapPage for the mod that handles logic for tooltips
and drawing everything.
Based on regurgitated game code.
*/

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Locations;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NPCMapLocations
{
  public class ModMapPage : MapPage
  {
    private Dictionary<string, bool> ConditionalNpcs { get; set; }
    private Dictionary<string, NpcMarker> NpcMarkers { get; set; }
    private Dictionary<long, FarmerMarker> FarmerMarkers { get; set; }
    private Dictionary<string, KeyValuePair<string, Vector2>> FarmBuildings { get; }

    private readonly Texture2D BuildingMarkers;
    private readonly ModCustomizations Customizations;
    private string hoveredNames = "";
    private string hoveredLocationText = "";
    private int mapX;
    private int mapY;
    private bool hasIndoorCharacter;
    private Vector2 indoorIconVector;
    private bool drawPamHouseUpgrade;
    private bool drawMovieTheaterJoja;
    private bool drawMovieTheater;
    private bool drawIsland;

    // Map menu that uses modified map page and modified component locations for hover
    public ModMapPage(
      Dictionary<string, NpcMarker> npcMarkers,
      Dictionary<string, bool> conditionalNpcs,
      Dictionary<long, FarmerMarker> farmerMarkers,
      Dictionary<string, KeyValuePair<string, Vector2>> farmBuildings,
      Texture2D buildingMarkers,
      ModCustomizations customizations
    ) : base(Game1.viewport.Width / 2 - (800 + IClickableMenu.borderWidth * 2) / 2,
      Game1.viewport.Height / 2 - (600 + IClickableMenu.borderWidth * 2) / 2, 800 + IClickableMenu.borderWidth * 2,
      600 + IClickableMenu.borderWidth * 2)
    {
      this.NpcMarkers = npcMarkers;
      this.ConditionalNpcs = conditionalNpcs;
      this.FarmerMarkers = farmerMarkers;
      this.FarmBuildings = farmBuildings;
      this.BuildingMarkers = buildingMarkers;
      this.Customizations = customizations;
      Vector2 center = Utility.getTopLeftPositionForCenteringOnScreen(ModMain.Map.Bounds.Width * 4, 720);
      mapX = (int)center.X;
      mapY = (int)center.Y;

      var customTooltips = Customizations.Tooltips.ToList();

      foreach (var tooltip in customTooltips)
      {
        var vanillaTooltip = this.points.Find(x => x.name == tooltip.name);
        var customTooltip = new ClickableComponent(new Rectangle(
          mapX + tooltip.bounds.X,
          mapY + tooltip.bounds.Y,
          tooltip.bounds.Width,
          tooltip.bounds.Height
        ), tooltip.name);

        // Replace vanilla with custom
        if (vanillaTooltip != null)
        {
          vanillaTooltip = customTooltip;
        }
        else
        // If new custom location, add it
        {
          this.points.Add(customTooltip);
        }
      }

      // If two tooltip areas overlap, the one earlier in the list takes precendence
      // Reversing order allows custom tooltips to take precendence
      this.points.Reverse();
    }

    public override void performHoverAction(int x, int y)
    {
      hoveredLocationText = "";
      hoveredNames = "";

      foreach (ClickableComponent c in points)
      {
        if (c.containsPoint(x, y))
        {
          hoveredLocationText = c.name;
          break;
        }
      }

      hasIndoorCharacter = false;
      foreach (ClickableComponent current in points)
      {
        if (current.containsPoint(x, y))
        {
          hoveredLocationText = current.name;
          break;
        }
      }

      List<string> hoveredList = new List<string>();

      const int markerWidth = 32;
      const int markerHeight = 30;

      // Have to use special character to separate strings for Chinese
      string separator = LocalizedContentManager.CurrentLanguageCode.Equals(LocalizedContentManager.LanguageCode.zh)
        ? "，"
        : ", ";

      if (NpcMarkers != null)
      {
        foreach (var npcMarker in this.NpcMarkers)
        {
          Vector2 npcLocation = new Vector2(mapX + npcMarker.Value.MapX, mapY + npcMarker.Value.MapY);
          if (Game1.getMouseX() >= npcLocation.X && Game1.getMouseX() <= npcLocation.X + markerWidth &&
              Game1.getMouseY() >= npcLocation.Y && Game1.getMouseY() <= npcLocation.Y + markerHeight)
          {
            if (!npcMarker.Value.IsHidden && !(npcMarker.Value.Type == Character.Horse))
            {
              if (Customizations.Names.TryGetValue(npcMarker.Key, out var name))
              {
                hoveredList.Add(name);
              }
            }

            if (!LocationUtil.IsOutdoors(npcMarker.Value.LocationName) && !hasIndoorCharacter)
              hasIndoorCharacter = true;
          }
        }
      }

      if (Context.IsMultiplayer && FarmerMarkers != null)
      {
        foreach (var farMarker in FarmerMarkers.Values)
        {
          Vector2 farmerLocation = new Vector2(mapX + farMarker.MapX, mapY + farMarker.MapY);
          if (Game1.getMouseX() >= farmerLocation.X - markerWidth / 2
           && Game1.getMouseX() <= farmerLocation.X + markerWidth / 2
           && Game1.getMouseY() >= farmerLocation.Y - markerHeight / 2
           && Game1.getMouseY() <= farmerLocation.Y + markerHeight / 2)
          {
            hoveredList.Add(farMarker.Name);

            if (!LocationUtil.IsOutdoors(farMarker.LocationName) && !hasIndoorCharacter)
              hasIndoorCharacter = true;
          }
        }
      }

      if (hoveredList.Count > 0)
      {
        hoveredNames = hoveredList[0];
        for (int i = 1; i < hoveredList.Count; i++)
        {
          var lines = hoveredNames.Split('\n');
          if ((int)Game1.smallFont.MeasureString(lines[lines.Length - 1] + separator + hoveredList[i]).X >
              (int)Game1.smallFont.MeasureString("Home of Robin, Demetrius, Sebastian & Maru").X) // Longest string
          {
            hoveredNames += separator + Environment.NewLine;
            hoveredNames += hoveredList[i];
          }
          else
          {
            hoveredNames += separator + hoveredList[i];
          }
        }
      }
    }

    protected override void drawMiniPortraits(SpriteBatch b)
    {
      return;
    }

    // Draw location and name tooltips
    public override void draw(SpriteBatch b)
    {
      base.draw(b);
      DrawMarkers(b);

      int x = Game1.getMouseX() + Game1.tileSize / 2;
      int y = Game1.getMouseY() + Game1.tileSize / 2;
      int width;
      int height;
      int offsetY = 0;

      if (!hoveredLocationText.Equals(""))
      {
        IClickableMenu.drawHoverText(b, hoveredLocationText, Game1.smallFont, 0, 0, -1, null, -1, null, null, 0, -1, -1,
          -1, -1, 1f, null);
        int textLength = (int)Game1.smallFont.MeasureString(hoveredLocationText).X + Game1.tileSize / 2;
        width = Math.Max((int)Game1.smallFont.MeasureString(hoveredLocationText).X + Game1.tileSize / 2, textLength);
        height = (int)Math.Max(60, Game1.smallFont.MeasureString(hoveredLocationText).Y + 5 * Game1.tileSize / 8);
        if (x + width > Game1.viewport.Width)
        {
          x = Game1.viewport.Width - width;
          y += Game1.tileSize / 4;
        }

        if (ModMain.Config.NameTooltipMode == 1)
        {
          if (y + height > Game1.viewport.Height)
          {
            x += Game1.tileSize / 4;
            y = Game1.viewport.Height - height;
          }

          offsetY = 2 - Game1.tileSize;
        }
        else if (ModMain.Config.NameTooltipMode == 2)
        {
          if (y + height > Game1.viewport.Height)
          {
            x += Game1.tileSize / 4;
            y = Game1.viewport.Height - height;
          }

          offsetY = height - 4;
        }
        else
        {
          if (y + height > Game1.viewport.Height)
          {
            x += Game1.tileSize / 4;
            y = Game1.viewport.Height - height;
          }
        }

        // Draw name tooltip positioned around location tooltip
        DrawNames(b, hoveredNames, x, y, offsetY, height, ModMain.Config.NameTooltipMode);

        // Draw location tooltip
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), x, y, width, height,
          Color.White, 1f, false);
        b.DrawString(Game1.smallFont, hoveredLocationText,
          new Vector2((float)(x + Game1.tileSize / 4), (float)(y + Game1.tileSize / 4 + 4)) + new Vector2(2f, 2f),
          Game1.textShadowColor);
        b.DrawString(Game1.smallFont, hoveredLocationText,
          new Vector2((float)(x + Game1.tileSize / 4), (float)(y + Game1.tileSize / 4 + 4)) + new Vector2(0f, 2f),
          Game1.textShadowColor);
        b.DrawString(Game1.smallFont, hoveredLocationText,
          new Vector2((float)(x + Game1.tileSize / 4), (float)(y + Game1.tileSize / 4 + 4)) + new Vector2(2f, 0f),
          Game1.textShadowColor);
        b.DrawString(Game1.smallFont, hoveredLocationText,
          new Vector2((float)(x + Game1.tileSize / 4), (float)(y + Game1.tileSize / 4 + 4)), Game1.textColor * 0.9f);
      }
      else
      {
        // Draw name tooltip only
        DrawNames(Game1.spriteBatch, hoveredNames, x, y, offsetY, this.height, ModMain.Config.NameTooltipMode);
      }

      // Draw indoor icon
      if (hasIndoorCharacter && !String.IsNullOrEmpty(hoveredNames))
        b.Draw(Game1.mouseCursors, indoorIconVector, new Rectangle?(new Rectangle(448, 64, 32, 32)), Color.White, 0f,
          Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

      // Cursor
      if (!Game1.options.hardwareCursor)
        b.Draw(Game1.mouseCursors, new Vector2(Game1.getOldMouseX(), Game1.getOldMouseY()),
          new Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors,
            (Game1.options.gamepadControls ? 44 : 0), 16, 16)), Color.White, 0f, Vector2.Zero,
          Game1.pixelZoom + Game1.dialogueButtonScale / 150f, SpriteEffects.None, 1f);
    }

    // Draw event
    // Subtractions within location vectors are to set the origin to the center of the sprite
    public void DrawMarkers(SpriteBatch b)
    {
      if (ModMain.Globals.ShowFarmBuildings && FarmBuildings != null && BuildingMarkers != null)
      {
        var sortedBuildings = FarmBuildings.ToList();
        sortedBuildings.Sort((x, y) => x.Value.Value.Y.CompareTo(y.Value.Value.Y));

        foreach (KeyValuePair<string, KeyValuePair<string, Vector2>> building in sortedBuildings)
        {
          if (ModConstants.FarmBuildingRects.TryGetValue(building.Value.Key, out Rectangle buildingRect))
          {
            b.Draw(
              BuildingMarkers,
              new Vector2(
                mapX + building.Value.Value.X - buildingRect.Width / 2,
                mapY + building.Value.Value.Y - buildingRect.Height / 2
              ),
              new Rectangle?(buildingRect), Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 1f
            );
          }
        }
      }

      // Traveling Merchant
      if (ModMain.Config.ShowTravelingMerchant && ConditionalNpcs["Merchant"])
      {
        Vector2 merchantLoc = new Vector2(ModConstants.MapVectors["Merchant"][0].MapX, ModConstants.MapVectors["Merchant"][0].MapY);
        b.Draw(Game1.mouseCursors, new Vector2(mapX + merchantLoc.X - 16, mapY + merchantLoc.Y - 15),
          new Rectangle?(new Rectangle(191, 1410, 22, 21)), Color.White, 0f, Vector2.Zero, 1.3f, SpriteEffects.None,
          1f);
      }

      // NPCs
      // Sort by drawing order
      if (NpcMarkers != null)
      {
        var sortedMarkers = NpcMarkers.ToList();
        sortedMarkers.Sort((x, y) => x.Value.Layer.CompareTo(y.Value.Layer));

        foreach (var npcMarker in sortedMarkers)
        {
          var name = npcMarker.Key;
          var marker = npcMarker.Value;

          // Skip if no specified location or should be hidden
          if (marker.Sprite == null
            || ModMain.Globals.NpcBlacklist.Contains(name)
            || (!ModMain.Config.ShowHiddenVillagers && marker.IsHidden)
            || (ConditionalNpcs.ContainsKey(name) && !ConditionalNpcs[name])
          )
          {
            continue;
          }

          // Dim marker for hidden markers
          var markerColor = marker.IsHidden ? Color.DarkGray * 0.7f : Color.White;

          // Draw NPC marker
          var spriteRect = marker.Type == Character.Horse ? new Rectangle(17, 104, 16, 14) : new Rectangle(0, marker.CropOffset, 16, 15);

          b.Draw(marker.Sprite,
            new Rectangle((int)(mapX + marker.MapX), (int)(mapY + marker.MapY),
              32, 30),
            new Rectangle?(spriteRect), markerColor);

          // Draw icons for quests/birthday
          if (ModMain.Config.MarkQuests)
          {
            if (marker.IsBirthday && (Game1.player.friendshipData.ContainsKey(name) && Game1.player.friendshipData[name].GiftsToday == 0))
            {
              // Gift icon
              b.Draw(Game1.mouseCursors,
                new Vector2(mapX + marker.MapX + 20, mapY + marker.MapY),
                new Rectangle?(new Rectangle(147, 412, 10, 11)), markerColor, 0f, Vector2.Zero, 1.8f,
                SpriteEffects.None, 0f);
            }

            if (marker.HasQuest)
            {
              // Quest icon
              b.Draw(Game1.mouseCursors,
                new Vector2(mapX + marker.MapX + 22, mapY + marker.MapY - 3),
                new Rectangle?(new Rectangle(403, 496, 5, 14)), markerColor, 0f, Vector2.Zero, 1.8f,
                SpriteEffects.None, 0f);
            }
          }
        }
      }

      // Farmers
      if (Context.IsMultiplayer)
      {
        foreach (Farmer farmer in Game1.getOnlineFarmers())
        {
          // Temporary solution to handle desync of farmhand location/tile position when changing location
          if (FarmerMarkers.TryGetValue(farmer.UniqueMultiplayerID, out FarmerMarker farMarker))
            if (farMarker == null)
              continue;
          if (farMarker != null && farMarker.DrawDelay == 0)
          {
            farmer.FarmerRenderer.drawMiniPortrat(b,
              new Vector2(mapX + farMarker.MapX - 16, mapY + farMarker.MapY - 15),
              0.00011f, 2f, 1, farmer);
          }
        }
      }
      else
      {
        Vector2 playerLoc = ModMain.LocationToMap(Game1.player.currentLocation.uniqueName.Value ?? Game1.player.currentLocation.Name, Game1.player.getTileX(),
          Game1.player.getTileY(), Customizations.MapVectors, Customizations.LocationBlacklist, true);

        Game1.player.FarmerRenderer.drawMiniPortrat(b,
          new Vector2(mapX + playerLoc.X - 16, mapY + playerLoc.Y - 15), 0.00011f, 2f, 1,
          Game1.player);
      }
    }

    // Draw NPC name tooltips map page
    public void DrawNames(SpriteBatch b, string names, int x, int y, int offsetY, int relocate, int nameTooltipMode)
    {
      if (hoveredNames.Equals("")) return;

      indoorIconVector = ModMain.UNKNOWN;
      var lines = names.Split('\n');
      int height = (int)Math.Max(60, Game1.smallFont.MeasureString(names).Y + Game1.tileSize / 2);
      int width = (int)Game1.smallFont.MeasureString(names).X + Game1.tileSize / 2;

      if (nameTooltipMode == 1)
      {
        x = Game1.getOldMouseX() + Game1.tileSize / 2;
        if (lines.Length > 1)
        {
          y += offsetY - ((int)Game1.smallFont.MeasureString(names).Y) + Game1.tileSize / 2;
        }
        else
        {
          y += offsetY;
        }

        // If going off screen on the right, move tooltip to below location tooltip so it can stay inside the screen
        // without the cursor covering the tooltip
        if (x + width > Game1.viewport.Width)
        {
          x = Game1.viewport.Width - width;
          if (lines.Length > 1)
          {
            y += relocate - 8 + ((int)Game1.smallFont.MeasureString(names).Y) + Game1.tileSize / 2;
          }
          else
          {
            y += relocate - 8 + Game1.tileSize;
          }
        }
      }
      else if (nameTooltipMode == 2)
      {
        y += offsetY;
        if (x + width > Game1.viewport.Width)
        {
          x = Game1.viewport.Width - width;
        }

        // If going off screen on the bottom, move tooltip to above location tooltip so it stays visible
        if (y + height > Game1.viewport.Height)
        {
          x = Game1.getOldMouseX() + Game1.tileSize / 2;
          if (lines.Length > 1)
          {
            y += -relocate + 8 - ((int)Game1.smallFont.MeasureString(names).Y) + Game1.tileSize / 2;
          }
          else
          {
            y += -relocate + 6 - Game1.tileSize;
          }
        }
      }
      else
      {
        x = Game1.activeClickableMenu.xPositionOnScreen - 145;
        y = Game1.activeClickableMenu.yPositionOnScreen + 650 - height / 2;
      }

      if (hasIndoorCharacter)
      {
        indoorIconVector = new Vector2(x - Game1.tileSize / 8 + 2, y - Game1.tileSize / 8 + 2);
      }

      Vector2 vector = new Vector2(x + (float)(Game1.tileSize / 4), y + (float)(Game1.tileSize / 4 + 4));

      drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), x, y, width, height, Color.White, 1f, true);
      b.DrawString(Game1.smallFont, names, vector + new Vector2(2f, 2f), Game1.textShadowColor, 0f, Vector2.Zero, 1f,
        SpriteEffects.None, 0f);
      b.DrawString(Game1.smallFont, names, vector + new Vector2(0f, 2f), Game1.textShadowColor, 0f, Vector2.Zero, 1f,
        SpriteEffects.None, 0f);
      b.DrawString(Game1.smallFont, names, vector + new Vector2(2f, 0f), Game1.textShadowColor, 0f, Vector2.Zero, 1f,
        SpriteEffects.None, 0f);
      b.DrawString(Game1.smallFont, names, vector, Game1.textColor * 0.9f, 0f, Vector2.Zero, 1f, SpriteEffects.None,
        0f);
    }
  }
}