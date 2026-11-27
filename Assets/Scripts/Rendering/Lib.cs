using System.Collections.Generic;
using UnityEngine;

namespace VoidStunts
{
    /// <summary>Every shader, texture and material the game uses, created once at boot from Resources.</summary>
    public static class Lib
    {
        public static Shader Surface, Foliage, Emissive, Glass, Sky, Plume, Tracks, Pulse;
        public static Texture2D Noise;
        static readonly Dictionary<string, Texture2D> cache = new();

        // world
        public static Material Concrete, ConcreteWarm, Asphalt, Facade, FacadeDark, Moss, Terrain, Steel, Dark, Bark, Leaves, Vines, Volt, Pink;
        public static Material GlassClear, GlassSmoke;
        public static Material LedWhite, LedWarm, LedPink, LedTeal, LedVolt, LedRed;
        // car
        public static Material Paint, Trim, Tyre, WheelFace, WheelCap, Graphite, Alu, Ink, Spring, Board, CellWhite, LensBlack, Nozzle, Core, CarLed, TailLed, TailX;

        public static Texture2D Tex(string name)
        {
            if (cache.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>("Textures/" + name);
            if (t == null) { Debug.LogWarning("VoidStunts: missing texture " + name); t = Texture2D.whiteTexture; }
            cache[name] = t; return t;
        }

        static Shader Find(string n)
        {
            var s = Resources.Load<Shader>("Shaders/" + n);
            if (s == null) s = Shader.Find("VoidStunts/" + n);
            if (s == null) Debug.LogError("VoidStunts: shader not found " + n);
            return s;
        }

        public static void Init()
        {
            if (Surface != null) return;
            Surface = Find("VSSurface"); Foliage = Find("VSFoliage"); Emissive = Find("VSUnlit"); Glass = Find("VSGlass");
            Sky = Find("VSSky"); Plume = Find("VSPlume"); Tracks = Find("VSTracks"); Pulse = Find("VSPulse");
            Noise = Tex("noise_m");

            Concrete = Surf("concrete", 3.2f, new Color(1, 1, 1), grime: .9f);
            ConcreteWarm = Surf("concrete", 3.2f, new Color(1.03f, .98f, .9f), grime: .7f);
            Asphalt = Surf("asphalt", 2.6f, Color.white, macro: 1.2f);
            Facade = Surf("concrete", 3.2f, new Color(.96f, .95f, .92f), grime: 1f); Facade.SetFloat("_Facade", 1);
            FacadeDark = Surf("concrete", 3.2f, new Color(.55f, .56f, .58f), grime: .8f); FacadeDark.SetFloat("_Facade", 1);
            Moss = Surf("moss", 2.2f, Color.white, macro: 1.3f);
            Terrain = Surf("sand", 4f, Color.white, macro: 1.4f); Terrain.SetFloat("_SlopeBlend", 1); Terrain.SetFloat("_Strata", 1);
            Shader.SetGlobalTexture("_VS_Rock2", Tex("rock_a")); Shader.SetGlobalTexture("_VS_Rock2N", Tex("rock_n")); Shader.SetGlobalTexture("_VS_Rock2M", Tex("rock_m"));
            Steel = Surf("steel", 2f, new Color(.42f, .43f, .45f), metal: .6f);
            Dark = Surf("steel", 2f, new Color(.14f, .145f, .15f), metal: .3f);
            Bark = Surf("bark", 1.6f, Color.white);
            Volt = Surf("concrete", 3.2f, new Color(.8f, .92f, .1f)); Volt.SetFloat("_Rough", .7f);
            Pink = Surf("concrete", 3f, new Color(.95f, .64f, .77f)); Pink.SetFloat("_Rough", .55f); Pink.SetFloat("_Macro", .3f);

            Leaves = new Material(Foliage) { name = "Leaves", enableInstancing = true };
            Leaves.SetTexture("_BaseMap", Tex("leaves_a")); Leaves.SetTexture("_NormalMap", Tex("leaves_n"));
            Vines = new Material(Leaves) { name = "Vines" }; Vines.SetFloat("_Wind", 1.6f);

            GlassClear = MakeGlass(new Color(.88f, .93f, .95f, .14f));
            GlassSmoke = MakeGlass(new Color(.85f, .9f, .92f, .22f));

            LedWhite = Led(new Color(1, 1, 1), 3.2f); LedWarm = Led(new Color(1f, .66f, .36f), 3f); LedPink = Led(new Color(1f, .38f, .62f), 3f);
            LedTeal = Led(new Color(.3f, 1f, .9f), 2.6f); LedVolt = Led(new Color(.83f, 1f, 0f), 2.4f); LedRed = Led(new Color(1f, .09f, .09f), 2.6f);

            // car (frozen VS-02 v0.8 materials)
            Paint = Surf(null, 1, Color.white, hex: false, macro: 0); Paint.SetFloat("_UVMode", 1);
            Trim = Surf(null, 1, new Color(.925f, .92f, .9f), hex: false, macro: 0); Trim.SetFloat("_Cull", 0);
            Tyre = Surf(null, 1, new Color(.086f, .086f, .09f), hex: false, macro: 0); Tyre.SetFloat("_Rough", .92f); Tyre.SetFloat("_Cull", 0);
            WheelFace = Solid(new Color(.933f, .933f, .918f), .45f, .1f); WheelCap = Solid(new Color(.09f, .094f, .102f), .4f, .2f);
            Graphite = Solid(new Color(.165f, .17f, .176f), .42f, .5f); Alu = Solid(new Color(.81f, .815f, .82f), .28f, 1f);
            Ink = Solid(new Color(.09f, .094f, .102f), .55f, .2f); Spring = Solid(new Color(.83f, 1f, 0f), .4f, .3f);
            CellWhite = Solid(new Color(.87f, .875f, .863f), .5f, 0f); LensBlack = Solid(new Color(.04f, .043f, .047f), .05f, .2f);
            Nozzle = Solid(new Color(.75f, .753f, .76f), .32f, 1f); Nozzle.SetFloat("_Cull", 0);
            Board = Surf(null, 1, Color.white, hex: false, macro: 0); Board.SetFloat("_UVMode", 1); Board.SetTexture("_BaseMap", Tex("board_a")); Board.SetFloat("_Rough", .6f);
            Core = Led(new Color(.06f, .06f, .065f), 1f); Core.SetFloat("_FarDim", 0);
            CarLed = Led(Color.white, 3f); CarLed.SetFloat("_FarDim", 0);
            TailLed = Led(Color.white, 1.65f); TailLed.SetFloat("_FarDim", 0);
            TailX = Led(new Color(1.4f, .12f, .12f), 1f); TailX.SetFloat("_FarDim", 0);
        }

        public static Material Surf(string set, float tiling, Color tint, float rough = 1, float metal = 0, bool hex = true, float macro = 1, float grime = 0)
        {
            var m = new Material(Surface) { enableInstancing = true, name = set ?? "Solid" };
            if (set != null)
            {
                m.SetTexture("_BaseMap", Tex(set + "_a")); m.SetTexture("_NormalMap", Tex(set + "_n")); m.SetTexture("_MaskMap", Tex(set + "_m"));
            }
            else
            {
                m.SetTexture("_NormalMap", FlatNormal()); m.SetTexture("_MaskMap", Texture2D.whiteTexture);
            }
            m.SetTexture("_NoiseMap", Noise);
            m.SetColor("_BaseColor", tint); m.SetFloat("_Tiling", tiling); m.SetFloat("_Hex", hex && set != null ? 1 : 0);
            m.SetFloat("_Macro", macro); m.SetFloat("_Rough", rough); m.SetFloat("_Metal", metal); m.SetFloat("_Grime", grime);
            m.SetFloat("_HexContrast", .35f); m.SetFloat("_NormalStrength", 1); m.SetFloat("_Cull", 2);
            return m;
        }
        public static Material Solid(Color c, float rough, float metal)
        {
            var m = Surf(null, 1, c, rough, metal, false, 0); return m;
        }
        public static Material Led(Color c, float intensity)
        {
            var m = new Material(Emissive) { enableInstancing = true, name = "Led" };
            m.SetColor("_BaseColor", c); m.SetFloat("_Intensity", intensity); m.SetFloat("_FarDim", 1); return m;
        }
        static Material MakeGlass(Color c)
        {
            var m = new Material(Glass) { name = "Glass", renderQueue = 3000 }; m.SetColor("_BaseColor", c); m.SetFloat("_Smooth", .95f); return m;
        }
        static Texture2D flat;
        static Texture2D FlatNormal()
        {
            if (flat != null) return flat;
            flat = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            var px = new Color32[16]; for (int i = 0; i < 16; i++) px[i] = new Color32(128, 128, 255, 255);
            flat.SetPixels32(px); flat.Apply(false, true); return flat;
        }
    }
}
