using System.Collections.Generic;
using System.Drawing;
using CNCMaps.Engine.Map;
using CNCMaps.Engine.Rendering;
using CNCMaps.FileFormats;
using CNCMaps.Shared;
using CNCMaps.VirtualFileSystem;

namespace CNCMaps.Engine.Game {
	public abstract class Drawable {
		static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

		public string Name { get; protected set; }
		internal IniFile.IniSection Rules { get; private set; }
		internal IniFile.IniSection Art { get; private set; }
		internal ObjectCollection OwnerCollection { get; set; }
		public DrawProperties Props = new DrawProperties();
		public readonly List<Drawable> SubDrawables = new List<Drawable>();

		public bool IsRemapable { get; set; }
		public bool InvisibleInGame { get; set; }
		public Point GlobalOffset { get; private set; }
		public Size Foundation { get; set; }

		public bool Overrides { get; set; }
		public bool IsWall { get; set; }
		public bool IsGate { get; set; }
		public bool IsVeins { get; set; }
		public bool IsVeinHoleMonster { get; set; }
		public int TileElevation { get; set; }
		public bool DrawFlat { get; set; }
		public bool Theater { get; set; }
		public bool IsValid { get; set; }

		public bool IsVoxel { get; set; }
		public bool NewTheater { get; set; }
		public string Image { get; set; }
		public bool TheaterExtension { get; set; }

		public static ushort TileWidth { get; set; }
		public static ushort TileHeight { get; set; }

		protected Drawable(IniFile.IniSection rules, IniFile.IniSection art) {
			Rules = rules;
			Art = art;
			Name = rules != null ? rules.Name : "";
			Foundation = new Size(1, 1);
		}

		public virtual void LoadFromRules() {
			Image = Art.ReadString("Image", Art.Name);
			IsVoxel = Art.ReadBool("Voxel");
			TheaterExtension = Art.ReadBool("Theater");

			NewTheater = OwnerCollection.Engine >= EngineType.RedAlert2 || Art.ReadBool("NewTheater");
			if (Art.ReadString("Remapable") != string.Empty) {
				// does NOT work in RA2
				if (OwnerCollection.Engine <= EngineType.Firestorm)
					IsRemapable = Art.ReadBool("Remapable");
			}

			// Used palet can be overriden
			bool noUseTileLandType = Rules.ReadString("NoUseTileLandType") != "";
			if (noUseTileLandType) {
				Props.PaletteType = PaletteType.Iso;
				Props.LightingType = LightingType.Full;
			}
			if (Art.ReadBool("TerrainPalette")) {
				Props.PaletteType = PaletteType.Iso;
				IsRemapable = false;
			}
			else if (Art.ReadBool("AnimPalette")) {
				Props.PaletteType = PaletteType.Anim;
				Props.LightingType = LightingType.None;
				IsRemapable = false;
			}
			else if (Art.ReadString("Palette") != string.Empty) {
				Props.PaletteType = PaletteType.Custom;
				Props.CustomPaletteName = Art.ReadString("Palette");
			}

			if (Rules.ReadString("AlphaImage") != "") {
				string alphaImageFile = Rules.ReadString("AlphaImage") + ".shp";
				if (VFS.Exists(alphaImageFile))
					SubDrawables.Add(new AlphaDrawable(Rules, Art, VFS.Open<ShpFile>(alphaImageFile)));
			}

			Props.HasShadow = Art.ReadBool("Shadow", Defaults.GetShadowAssumption(OwnerCollection.Type));
			DrawFlat = Rules.ReadBool("DrawFlat", Defaults.GetFlatnessAssumption(OwnerCollection.Type))
				|| Rules.ReadBool("Flat");

			if (Rules.ReadBool("Wall")) {
				IsWall = true;
				DrawFlat = false;
				// RA2 walls appear a bit higher
				if (OwnerCollection.Engine >= EngineType.RedAlert2) {
					Props.Offset.Offset(0, 3); // seems walls are located 3 pixels lower
				}
				Props.PaletteType = PaletteType.Unit;
				Props.LightingType = LightingType.Ambient;
				Props.FrameDecider = FrameDeciders.OverlayValueFrameDecider;
			}
			if (Rules.ReadBool("Gate")) {
				IsGate = true;
				DrawFlat = false;
				Props.PaletteType = PaletteType.Unit;
				Props.FrameDecider = FrameDeciders.NullFrameDecider;
			}

			if (Rules.ReadBool("IsVeins")) {
				Props.LightingType = LightingType.None;
				Props.PaletteType = PaletteType.Unit;
				IsVeins = true;
			}
			if (Rules.ReadBool("IsVeinholeMonster")) {
				Props.Offset.Y = -48; // why is this needed???
				Props.LightingType = LightingType.None;
				Props.PaletteType = PaletteType.Unit;
				IsVeinHoleMonster = true;
			}

			if (Rules.ReadString("Land") == "Rock") {
				Props.Offset.Y += TileHeight / 2;
				//mainProps.ZBufferAdjust += Drawable.TileHeight / 2;
			}
			else if (Rules.ReadString("Land") == "Road") {
				Props.Offset.Y += TileHeight / 2;
				// drawable.Foundation = new Size(3, 1); // ensures bridges are drawn a bit lower than where they're stored
			}
			else if (Rules.ReadString("Land") == "Railroad") {
				if (OwnerCollection.Engine <= EngineType.Firestorm)
					Props.Offset.Y = 11;
				else
					Props.Offset.Y = 14;
				Props.LightingType = LightingType.Full;
				Props.PaletteType = PaletteType.Iso;
				Foundation = new Size(2, 2); // hack to get these later in the drawing order
			}
			if (Rules.ReadBool("SpawnsTiberium")) {
				// For example on TIBTRE / Ore Poles
				Props.Offset.Y = -1;
				Props.LightingType = LightingType.None;
				Props.PaletteType = PaletteType.Unit;
			}
			if (Rules.HasKey("JumpjetHeight")) {
				Props.Offset.Offset(0, (int)(-Rules.ReadInt("JumpjetHeight") / 256.0 * Drawable.TileHeight));
			}
			Props.Offset.Offset(Art.ReadInt("XDrawOffset"), Art.ReadInt("YDrawOffset"));
		}

		public virtual void Draw(GameObject obj, DrawingSurface ds) {
			logger.Trace("Drawing object {0} (type {1})", obj, obj.GetType());
		}

		public virtual Rectangle GetBounds(GameObject obj) {
			Rectangle bounds = Rectangle.Empty;
			// TODO
			/*
			for (int i = 0; i < _voxels.Count; i++) {
				var vxl = _voxels[i];
				var hva = _hvas[i];
				var vxlbounds = vxl.File.GetBounds(obj, vxl.File, hva, vxl.Props);
				bounds = Rectangle.Union(bounds, vxlbounds);
			}
			foreach (var shp in _shps.Where(shp => shp.File != null)) {
				if (bounds == Rectangle.Empty) bounds = shp.File.GetBounds(obj, shp.Props);
				else bounds = Rectangle.Union(bounds, shp.File.GetBounds(obj, shp.Props));
			}
			bounds.Offset(obj.Tile.Dx * TileWidth / 2, (obj.Tile.Dy - obj.Tile.Z) * TileHeight / 2);
			bounds.Offset(GlobalOffset);

			if (obj is OwnableObject && (obj as OwnableObject).OnBridge)
				bounds.Offset(0, -4 * TileHeight / 2);
			*/
			return bounds;
		}

		private static readonly Pen BoundsRectPenVoxel = new Pen(Color.Blue);
		private static readonly Pen BoundsRectPenSHP = new Pen(Color.Red);
		public void DrawBoundingBox(GameObject obj, Graphics gfx) {
			if (IsVoxel)
				gfx.DrawRectangle(BoundsRectPenVoxel, GetBounds(obj));
			else
				gfx.DrawRectangle(BoundsRectPenSHP, GetBounds(obj));
		}


		internal Drawable Clone() {
			return (Drawable)MemberwiseClone();
		}

		public override string ToString() {
			return Name;
		}

	}
}