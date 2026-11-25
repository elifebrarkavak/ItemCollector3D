using System.IO;
using UnityEditor;
using UnityEngine;
using static ItemCollector3D.EditorTools.Raster;
using static ItemCollector3D.EditorTools.UiPalette;

namespace ItemCollector3D.EditorTools
{
    /// <summary>
    /// Generates the whole Item Collector 3D UI sprite set (chunky buttons, wood panels, gold sliders, icons) from code, so the design
    /// system is reproducible and needs no external art or new packages. Output: Assets/UI/Sprites/Generated.
    /// </summary>
    public static class UiSpriteFactory
    {
        public const string Folder = "Assets/UI/Sprites/Generated";

        public static Sprite Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");
        }

        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);

            // ------------------------------------------------ buttons (256x128, 9-slice)
            Save("ui_btn_gold", Button(256, 128, 46, GoldLight, Gold, GoldDark, Hex("#FFF3C4")), 56);
            Save("ui_btn_wood", Button(256, 128, 46, Hex("#9C6746"), WoodBrown, WoodDark, Hex("#C48A62")), 56);
            Save("ui_btn_green", Button(256, 128, 46, GreenLight, NaturalGreen, GreenDark, Hex("#9BE0C4")), 56);
            Save("ui_btn_navy", Button(256, 128, 46, NavyLight, Hex("#26364E"), NavyDeep, Hex("#5C7396")), 56);

            // ------------------------------------------------ round buttons (128x128)
            Save("ui_round_navy", RoundButton(128, NavyLight, Hex("#26364E"), NavyDeep, Hex("#5C7396")), 0);
            Save("ui_round_wood", RoundButton(128, Hex("#9C6746"), WoodBrown, WoodDark, Hex("#C48A62")), 0);
            Save("ui_round_gold", RoundButton(128, GoldLight, Gold, GoldDark, Hex("#FFF3C4")), 0);

            // ------------------------------------------------ panels
            Save("ui_panel_wood", WoodPanel(256, 256), 72);
            Save("ui_row_inset", RowInset(128, 128), 46);
            Save("ui_banner", Banner(256, 128), 60);
            Save("ui_badge", Badge(128, 128), 44);
            Save("ui_shadow", SoftShadow(128, 128), 56);
            Save("ui_plate", NamePlate(256, 112), 48);

            // ------------------------------------------------ slider
            Save("ui_slider_track", SliderTrack(128, 64), 30);
            Save("ui_slider_fill", SliderFill(128, 64), 28);
            Save("ui_slider_knob", SliderKnob(96), 0);

            // ------------------------------------------------ icons (128x128, cream with navy outline)
            Save("icon_music", Icon(Union(Ellipse(38, 94, 19, 14), Ellipse(88, 82, 19, 14), Capsule(54, 90, 54, 34, 5.5f), Capsule(104, 78, 104, 22, 5.5f),
                Polygon(new Vector2(49, 28), new Vector2(109, 14), new Vector2(109, 36), new Vector2(49, 50))), Cream), 0);
            Save("icon_sfx", Icon(Union(
                Polygon(new Vector2(12, 48), new Vector2(32, 48), new Vector2(58, 22), new Vector2(58, 106), new Vector2(32, 80), new Vector2(12, 80)),
                Intersect(Ring(52, 64, 26, 5f), Polygon(new Vector2(52, 64), new Vector2(150, -34), new Vector2(150, 162))),
                Intersect(Ring(52, 64, 42, 5f), Polygon(new Vector2(52, 64), new Vector2(150, -34), new Vector2(150, 162)))), Cream), 0);
            Save("icon_camera", IconCamera(), 0);
            Save("icon_play", Icon(Grow(Polygon(new Vector2(46, 34), new Vector2(46, 94), new Vector2(94, 64)), 10), Cream), 0);
            Save("icon_gear", Icon(Subtract(Union(Circle(64, 64, 34), Capsule(64, 64, 64, 12, 9), Capsule(64, 64, 64, 116, 9), Capsule(64, 64, 12, 64, 9), Capsule(64, 64, 116, 64, 9),
                Capsule(64, 64, 28, 28, 9), Capsule(64, 64, 100, 100, 9), Capsule(64, 64, 100, 28, 9), Capsule(64, 64, 28, 100, 9)), Circle(64, 64, 14)), Cream), 0);
            Save("icon_helmet", IconHelmet(), 0);
            Save("icon_exit", Icon(Union(
                Subtract(Ring(64, 70, 34, 7f), Polygon(new Vector2(64, 70), new Vector2(30, -10), new Vector2(98, -10))), Capsule(64, 22, 64, 62, 7.5f)), Cream), 0);
            Save("icon_chev_l", Icon(Union(Capsule(82, 22, 44, 64, 12), Capsule(44, 64, 82, 106, 12)), Cream), 0);
            Save("icon_chev_r", Icon(Union(Capsule(46, 22, 84, 64, 12), Capsule(84, 64, 46, 106, 12)), Cream), 0);
            Save("icon_close", Icon(Union(Capsule(36, 36, 92, 92, 12), Capsule(92, 36, 36, 92, 12)), Cream), 0);
            Save("icon_check", IconColored(Union(Capsule(30, 68, 54, 92, 12), Capsule(54, 92, 100, 38, 12)), Cream, Outline), 0);
            Save("icon_pause", Icon(Union(RoundRectBox(36, 30, 58, 98, 7), RoundRectBox(70, 30, 92, 98, 7)), Cream), 0);
            Save("icon_gem", IconGem(), 0);

            // ------------------------------------------------ misc
            Save("ui_glow", Glow(128), 0);
            Save("ui_vignette", Vignette(256), 0);
            Save("ui_wood_grain", WoodGrain(256), 0, tile: true);
            Save("ui_dot", Dot(48), 0);

            BuildHudSprites();
            BuildAreaSprites();
            BuildCelebrationSprites();
            AssetDatabase.Refresh();
        }

        /// <summary>Sprites of the LEVEL COMPLETE panel (stage 17C): the gold trophy and a small star. Only generates what is new, the rest of the set is untouched.</summary>
        public static void BuildCelebrationSprites()
        {
            Directory.CreateDirectory(Folder);
            Save("icon_trophy", IconTrophy(), 0);
            Save("icon_star", IconStar(), 0);
            AssetDatabase.Refresh();
        }

        /// <summary>Sprites of the gameplay HUD (stage 9): pickaxe icon, translucent joystick, gold progress fill, signal-meter pill.</summary>
        public static void BuildHudSprites()
        {
            Directory.CreateDirectory(Folder);
            Save("icon_pickaxe", IconPickaxe(), 0);
            Save("ui_joy_base", JoystickBase(256), 0);
            Save("ui_joy_knob", JoystickKnob(128), 0);
            Save("ui_bar_fill", BarFill(64, 64), 0);
            Save("ui_pill", Pill(64, 64), 22);
            Save("ui_shade_top", Shade(8, 64, false), 0); // soft gradients: dark at the top / left, clear at the other end (stretched under menu text)
            Save("ui_shade_left", Shade(64, 8, true), 0);
            AssetDatabase.Refresh();
        }

        /// <summary>Sprites of the Area Selection (stage 11): the padlock of a locked area card / button. Only generates what is new, the rest of the set is untouched.</summary>
        public static void BuildAreaSprites()
        {
            Directory.CreateDirectory(Folder);
            Sdf shackle = Intersect(Ring(64, 54, 21f, 7f), (x, y) => y - 58f);
            Sdf body = RoundRectBox(30, 54, 98, 108, 13);
            Sdf keyhole = Union(Circle(64, 76, 8.5f), Polygon(new Vector2(59, 80), new Vector2(69, 80), new Vector2(72, 96), new Vector2(56, 96)));
            Save("icon_lock", Icon(Subtract(Union(shackle, body), keyhole), Cream), 0);
            AssetDatabase.Refresh();
        }

        // ================================================================ shapes

        private static Raster Button(int w, int h, float r, Color faceTop, Color faceBottom, Color lip, Color highlight, float lipH = 16f)
        {
            var rs = new Raster(w, h);
            const float o = 4f;
            rs.Fill(RoundRectBox(0, 0, w, h, r), Outline);
            rs.Fill(RoundRectBox(o, o, w - o, h - o, r - o), lip);
            float faceBottom_ = h - o - lipH;
            Sdf face = RoundRectBox(o, o, w - o, faceBottom_, r - o);
            rs.Fill(face, VerticalGradient(o, faceBottom_, faceTop, faceBottom));
            // darker seam between face and lip
            rs.Fill(Intersect(Subtract(Grow(face, 2.5f), face), (x, y) => (h * 0.5f) - y), Alpha(Color.Lerp(lip, Outline, 0.35f), 0.9f));
            // glossy upper half + bright top rim
            rs.Fill(Intersect(face, (x, y) => y - (o + (faceBottom_ - o) * 0.5f)), new Color(1f, 1f, 1f, 0f));
            rs.Fill(Intersect(face, (x, y) => (o + (faceBottom_ - o) * 0.46f) - y), new Color(1f, 1f, 1f, 0.10f));
            rs.Fill(Subtract(face, Offset(face, 0f, 4.5f)), Alpha(highlight, 0.85f));
            return rs;
        }

        private static Raster RoundButton(int s, Color faceTop, Color faceBottom, Color lip, Color highlight)
        {
            var rs = new Raster(s, s);
            float c = s * 0.5f;
            rs.Fill(Circle(c, c, c), Outline);
            rs.Fill(Circle(c, c, c - 4f), lip);
            Sdf face = Circle(c, c - 5f, c - 4f - 4f);
            rs.Fill(face, VerticalGradient(8f, s - 14f, faceTop, faceBottom));
            rs.Fill(Intersect(Subtract(Grow(face, 2.5f), face), (x, y) => c - y + 12f), Alpha(Color.Lerp(lip, Outline, 0.35f), 0.9f));
            rs.Fill(Subtract(face, Offset(face, 0f, 4.5f)), Alpha(highlight, 0.85f));
            return rs;
        }

        private static Raster WoodPanel(int w, int h)
        {
            // stage 10: a slimmer frame (the wood rim is 14 px, was 21) and a thin groove, so the panel reads lighter and the content gets more room
            var rs = new Raster(w, h);
            rs.Fill(RoundRectBox(0, 0, w, h, 52), Outline);
            Sdf frame = RoundRectBox(4, 4, w - 4, h - 4, 48);
            rs.Fill(frame, VerticalGradient(4, h - 4, WoodLight, Hex("#734A31")));
            rs.Fill(Subtract(frame, Offset(frame, 0f, 3f)), Alpha(Hex("#D9A272"), 0.5f));
            // groove around the board
            rs.Fill(RoundRectBox(18, 18, w - 18, h - 18, 34), WoodDeepest);
            Sdf board = RoundRectBox(21, 21, w - 21, h - 21, 31);
            rs.Fill(board, VerticalGradient(21, h - 21, Hex("#6E4533"), Hex("#563424")));
            rs.Fill(Subtract(board, Offset(board, 0f, 12f)), new Color(0f, 0f, 0f, 0.20f)); // inner shadow, board looks recessed
            rs.Fill(Subtract(Offset(board, 0f, 2f), board), Alpha(Hex("#9A6A4C"), 0.40f)); // lower lip catches light
            return rs;
        }

        private static Raster RowInset(int w, int h)
        {
            var rs = new Raster(w, h);
            rs.Fill(RoundRectBox(0, 0, w, h, 44), Hex("#1E0F08"));
            Sdf body = RoundRectBox(3, 3, w - 3, h - 3, 41);
            rs.Fill(body, VerticalGradient(3, h - 3, Hex("#3F2518"), Hex("#4A2C1D")));
            rs.Fill(Subtract(body, Offset(body, 0f, 10f)), new Color(0f, 0f, 0f, 0.30f));
            rs.Fill(Subtract(Offset(body, 0f, 2f), body), Alpha(Hex("#9A6A4C"), 0.35f));
            return rs;
        }

        private static Raster Banner(int w, int h)
        {
            var rs = new Raster(w, h);
            rs.Fill(RoundRectBox(0, 0, w, h, 40), Outline);
            Sdf body = RoundRectBox(4, 4, w - 4, h - 4, 36);
            rs.Fill(body, VerticalGradient(4, h - 4, Hex("#33476A"), CaveNavy));
            rs.Fill(Subtract(RoundRectBox(12, 12, w - 12, h - 12, 28), RoundRectBox(16, 16, w - 16, h - 16, 24)), Alpha(Gold, 0.9f));
            rs.Fill(Subtract(body, Offset(body, 0f, 4f)), Alpha(Hex("#6F8AB5"), 0.6f));
            return rs;
        }

        private static Raster Badge(int w, int h)
        {
            var rs = new Raster(w, h);
            rs.Fill(RoundRectBox(0, 0, w, h, 38), Outline);
            Sdf body = RoundRectBox(4, 4, w - 4, h - 4, 34);
            rs.Fill(body, VerticalGradient(4, h - 4, GreenLight, GreenDark));
            rs.Fill(Subtract(body, Offset(body, 0f, 4f)), Alpha(Hex("#BDEBD7"), 0.7f));
            rs.Fill(Subtract(RoundRectBox(11, 11, w - 11, h - 11, 28), RoundRectBox(14, 14, w - 14, h - 14, 25)), Alpha(Cream, 0.85f));
            return rs;
        }

        private static Raster SoftShadow(int w, int h)
        {
            var rs = new Raster(w, h);
            Sdf box = RoundRectBox(30, 30, w - 30, h - 30, 24);
            rs.FillAll((x, y) => new Color(0f, 0f, 0f, 0.55f * Mathf.Clamp01(0.5f - box(x, y) / 26f)));
            return rs;
        }

        private static Raster NamePlate(int w, int h)
        {
            var rs = new Raster(w, h);
            rs.Fill(RoundRectBox(0, 0, w, h, 38), Outline);
            Sdf body = RoundRectBox(4, 4, w - 4, h - 4, 34);
            rs.Fill(body, VerticalGradient(4, h - 4, Hex("#8A5A3C"), Hex("#6B4129")));
            rs.Fill(Subtract(body, Offset(body, 0f, 4f)), Alpha(Hex("#D9A272"), 0.55f));
            rs.Fill(Subtract(Offset(body, 0f, 3f), body), new Color(0f, 0f, 0f, 0.25f));
            return rs;
        }

        private static Raster SliderTrack(int w, int h)
        {
            var rs = new Raster(w, h);
            rs.Fill(RoundRectBox(0, 0, w, h, h * 0.5f), Outline);
            Sdf body = RoundRectBox(4, 4, w - 4, h - 4, h * 0.5f - 4f);
            rs.Fill(body, VerticalGradient(4, h - 4, Hex("#2A170E"), Hex("#4A2B1B")));
            rs.Fill(Subtract(body, Offset(body, 0f, 9f)), new Color(0f, 0f, 0f, 0.35f));
            rs.Fill(Subtract(Offset(body, 0f, 2f), body), Alpha(Hex("#9A6A4C"), 0.35f));
            return rs;
        }

        private static Raster SliderFill(int w, int h)
        {
            var rs = new Raster(w, h);
            rs.Fill(RoundRectBox(0, 0, w, h, h * 0.5f), GoldDeep);
            Sdf body = RoundRectBox(3, 3, w - 3, h - 3, h * 0.5f - 3f);
            rs.Fill(body, VerticalGradient(3, h - 3, GoldLight, GoldDark));
            rs.Fill(Intersect(body, (x, y) => (h * 0.5f) - y), new Color(1f, 1f, 1f, 0.12f));
            rs.Fill(Subtract(body, Offset(body, 0f, 5f)), Alpha(Hex("#FFF3C4"), 0.85f));
            return rs;
        }

        private static Raster SliderKnob(int s)
        {
            var rs = new Raster(s, s);
            float c = s * 0.5f;
            rs.Fill(Circle(c, c + 3f, c - 1f), Outline);
            rs.Fill(Circle(c, c, c - 3f), Outline);
            rs.Fill(Circle(c, c, c - 6f), VerticalGradient(6, s - 6, GoldLight, GoldDark));
            rs.Fill(Circle(c, c + 1f, c - 21f), GoldDeep);
            rs.Fill(Circle(c, c - 1f, c - 23f), VerticalGradient(c - 24f, c + 24f, Hex("#FFF8E6"), Cream));
            rs.Fill(Ellipse(c - 6f, c - 15f, 6f, 3.5f), new Color(1f, 1f, 1f, 0.8f));
            return rs;
        }

        // ================================================================ icons

        private static Raster Icon(Sdf shape, Color fill)
        {
            return IconColored(shape, fill, Outline);
        }

        private static Raster IconColored(Sdf shape, Color fill, Color outline)
        {
            var rs = new Raster(128, 128);
            rs.Fill(Grow(Offset(shape, 0f, 3f), 5f), new Color(0f, 0f, 0f, 0.28f));
            rs.Fill(Grow(shape, 5f), outline);
            rs.Fill(shape, fill);
            return rs;
        }

        private static Raster IconCamera()
        {
            var rs = new Raster(128, 128);
            Sdf body = Union(RoundRectBox(12, 40, 116, 104, 16), RoundRectBox(38, 26, 80, 48, 8));
            rs.Fill(Grow(Offset(body, 0f, 3f), 5f), new Color(0f, 0f, 0f, 0.28f));
            rs.Fill(Grow(body, 5f), Outline);
            rs.Fill(body, Cream);
            rs.Fill(Circle(64, 72, 25), Hex("#9C6746"));
            rs.Fill(Circle(64, 72, 20), CaveNavy);
            rs.Fill(Circle(64, 72, 12), Hex("#31445F"));
            rs.Fill(Circle(58, 66, 4.5f), Cream);
            rs.Fill(RoundRectBox(92, 48, 106, 56, 3), Gold);
            return rs;
        }

        private static Raster IconHelmet()
        {
            var rs = new Raster(128, 128);
            Sdf dome = Intersect(Circle(64, 76, 42), Polygon(new Vector2(0, 0), new Vector2(128, 0), new Vector2(128, 76), new Vector2(0, 76)));
            Sdf brim = RoundRectBox(14, 72, 114, 90, 9);
            Sdf all = Union(dome, brim);
            rs.Fill(Grow(Offset(all, 0f, 3f), 5f), new Color(0f, 0f, 0f, 0.28f));
            rs.Fill(Grow(all, 5f), Outline);
            rs.Fill(dome, VerticalGradient(34, 76, GoldLight, Gold));
            rs.Fill(brim, VerticalGradient(72, 90, Gold, GoldDark));
            rs.Fill(Capsule(64, 38, 64, 74, 6f), GoldDark);
            rs.Fill(Circle(64, 62, 12), Outline);
            rs.Fill(Circle(64, 62, 8.5f), Cream);
            return rs;
        }

        private static Raster IconGem()
        {
            var rs = new Raster(128, 128);
            Sdf all = Polygon(new Vector2(26, 42), new Vector2(50, 14), new Vector2(78, 14), new Vector2(102, 42), new Vector2(64, 116));
            rs.Fill(Grow(Offset(all, 0f, 3f), 5f), new Color(0f, 0f, 0f, 0.28f));
            rs.Fill(Grow(all, 5f), Outline);
            rs.Fill(all, Gold);
            rs.Fill(Polygon(new Vector2(26, 42), new Vector2(50, 14), new Vector2(60, 42)), GoldLight);
            rs.Fill(Polygon(new Vector2(50, 14), new Vector2(78, 14), new Vector2(68, 42), new Vector2(60, 42)), Hex("#FFF3C4"));
            rs.Fill(Polygon(new Vector2(78, 14), new Vector2(102, 42), new Vector2(68, 42)), GoldLight);
            rs.Fill(Polygon(new Vector2(26, 42), new Vector2(60, 42), new Vector2(64, 116)), Gold);
            rs.Fill(Polygon(new Vector2(102, 42), new Vector2(68, 42), new Vector2(64, 116)), GoldDark);
            rs.Fill(Polygon(new Vector2(60, 42), new Vector2(68, 42), new Vector2(64, 116)), Hex("#E0A12B"));
            return rs;
        }

        private static Raster IconTrophy()
        {
            var rs = new Raster(128, 128);
            // the cup: a rim, a rounded bowl, two ring handles, a neck and a two-step base
            Sdf bowl = Union(RoundRectBox(30, 12, 98, 28, 6), Intersect(Circle(64, 36, 35), (x, y) => 20f - y));
            Sdf handleL = Intersect(Ring(31, 38, 14f, 5f), (x, y) => x - 36f);
            Sdf handleR = Intersect(Ring(97, 38, 14f, 5f), (x, y) => 92f - x);
            Sdf neck = Polygon(new Vector2(55, 66), new Vector2(73, 66), new Vector2(77, 90), new Vector2(51, 90));
            Sdf baseTop = RoundRectBox(38, 88, 90, 104, 6);
            Sdf baseBottom = RoundRectBox(28, 102, 100, 118, 6);
            Sdf all = Union(bowl, handleL, handleR, neck, baseTop, baseBottom);
            rs.Fill(Grow(Offset(all, 0f, 3f), 5f), new Color(0f, 0f, 0f, 0.28f));
            rs.Fill(Grow(all, 5f), Outline);
            rs.Fill(Union(handleL, handleR), Gold);
            rs.Fill(bowl, HorizontalGradient(30, 98, GoldLight, GoldDark));
            rs.Fill(neck, HorizontalGradient(51, 77, Gold, GoldDark));
            rs.Fill(baseTop, HorizontalGradient(38, 90, Gold, GoldDark));
            rs.Fill(baseBottom, HorizontalGradient(28, 100, GoldLight, GoldDeep));
            rs.Fill(Subtract(bowl, Offset(bowl, 0f, 4f)), Hex("#FFF3C4")); // bright rim
            rs.Fill(RoundRectBox(40, 30, 49, 56, 4), new Color(1f, 1f, 1f, 0.55f)); // shine on the bowl
            rs.Fill(Polygon(new Vector2(64, 30), new Vector2(68, 40), new Vector2(79, 41), new Vector2(70, 48), new Vector2(73, 59), new Vector2(64, 53), new Vector2(55, 59), new Vector2(58, 48), new Vector2(49, 41), new Vector2(60, 40)), Hex("#FFF3C4")); // star on the cup
            return rs;
        }

        private static Raster IconStar()
        {
            var rs = new Raster(128, 128);
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = -Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 56f : 25f;
                points[i] = new Vector2(64f + Mathf.Cos(angle) * r, 68f + Mathf.Sin(angle) * r);
            }

            Sdf star = Polygon(points);
            rs.Fill(Grow(Offset(star, 0f, 3f), 5f), new Color(0f, 0f, 0f, 0.28f));
            rs.Fill(Grow(star, 5f), Outline);
            rs.Fill(star, VerticalGradient(12f, 124f, Hex("#FFF3C4"), Gold));
            return rs;
        }

        private static Raster IconPickaxe()
        {
            var rs = new Raster(128, 128);
            // handle from the lower left up to the middle of the head; the head is a thick crescent across the handle's top end
            Vector2 up = new Vector2(52f, -68f).normalized;
            var centre = new Vector2(60f, 68f);
            Sdf handle = Capsule(26, 110, centre.x + up.x * 52f, centre.y + up.y * 52f, 8f);
            Vector2 innerCentre = centre - up * 18f;
            Sdf crescent = Intersect(
                Subtract(Circle(centre.x, centre.y, 62f), Circle(innerCentre.x, innerCentre.y, 57f)),
                (x, y) => -((x - centre.x) * up.x + (y - centre.y) * up.y) - 6f);
            Sdf all = Union(handle, crescent);
            rs.Fill(Grow(Offset(all, 0f, 3f), 5f), new Color(0f, 0f, 0f, 0.28f));
            rs.Fill(Grow(all, 5f), Outline);
            rs.Fill(handle, VerticalGradient(30, 110, Hex("#D9A272"), Hex("#9A6844")));
            rs.Fill(crescent, VerticalGradient(10, 100, Hex("#FFFFFF"), Hex("#C9D5E4")));
            rs.Fill(Subtract(crescent, Offset(crescent, 0f, 4f)), new Color(1f, 1f, 1f, 0.9f));
            return rs;
        }

        private static Raster JoystickBase(int s)
        {
            var rs = new Raster(s, s);
            float c = s * 0.5f;
            rs.Fill(Circle(c, c, c - 4f), Alpha(NavyDeep, 0.46f)); // translucent cave-navy disc
            rs.Fill(Circle(c, c, 56f), new Color(0f, 0f, 0f, 0.16f)); // the dock the knob rests in
            rs.Fill(Ring(c, c, c - 6f, 3.5f), Alpha(Cream, 0.82f)); // outer rim
            rs.Fill(Ring(c, c, 78f, 1.6f), Alpha(Cream, 0.20f)); // inner guide ring
            for (int i = 0; i < 4; i++) // direction chevrons
            {
                float a = i * Mathf.PI * 0.5f;
                Vector2 Rot(float dx, float dy) => new Vector2(c + dx * Mathf.Cos(a) - dy * Mathf.Sin(a), c + dx * Mathf.Sin(a) + dy * Mathf.Cos(a));
                rs.Fill(Polygon(Rot(-13f, -88f), Rot(13f, -88f), Rot(0f, -108f)), Alpha(Cream, 0.78f));
            }

            return rs;
        }

        private static Raster JoystickKnob(int s)
        {
            var rs = new Raster(s, s);
            float c = s * 0.5f;
            rs.Fill(Circle(c, c + 3f, c - 3f), new Color(0f, 0f, 0f, 0.28f));
            rs.Fill(Circle(c, c, c - 5f), Alpha(Outline, 0.9f));
            rs.Fill(Circle(c, c, c - 9f), VerticalGradient(9, s - 9, GoldLight, GoldDark));
            rs.Fill(Circle(c, c + 3f, c - 27f), Alpha(GoldDeep, 0.30f)); // soft dish in the middle
            rs.Fill(Circle(c, c + 1f, c - 31f), VerticalGradient(30, s - 30, Hex("#FFE9A3"), GoldLight));
            rs.Fill(Ellipse(c - 7f, c - 25f, 19f, 8f), new Color(1f, 1f, 1f, 0.62f));
            return rs;
        }

        private static Raster BarFill(int w, int h)
        {
            var rs = new Raster(w, h);
            rs.FillAll((x, y) =>
            {
                float t = y / h;
                Color c = Color.Lerp(GoldLight, GoldDark, Mathf.Clamp01(t * 1.15f));
                if (t < 0.26f) { c = Color.Lerp(c, Hex("#FFF3C4"), 0.55f * (1f - t / 0.26f)); }
                if (t > 0.86f) { c = Color.Lerp(c, GoldDeep, (t - 0.86f) / 0.14f * 0.8f); }
                return c;
            });
            return rs;
        }

        private static Raster Shade(int w, int h, bool horizontal)
        {
            var rs = new Raster(w, h);
            rs.FillAll((x, y) =>
            {
                float t = horizontal ? x / w : y / h; // 0 = the dark end (image space, y down: the top row)
                return new Color(0.03f, 0.05f, 0.09f, Mathf.Pow(1f - Mathf.Clamp01(t), 1.6f));
            });
            return rs;
        }

        private static Raster Pill(int w, int h)
        {
            var rs = new Raster(w, h);
            rs.Fill(RoundRectBox(0, 0, w, h, 22), Outline);
            Sdf body = RoundRectBox(3, 3, w - 3, h - 3, 19);
            rs.Fill(body, VerticalGradient(3, h - 3, Color.white, new Color(0.78f, 0.78f, 0.78f, 1f)));
            rs.Fill(Subtract(body, Offset(body, 0f, 4f)), new Color(1f, 1f, 1f, 0.55f));
            return rs;
        }

        // ================================================================ misc

        private static Raster Glow(int s)
        {
            var rs = new Raster(s, s);
            float c = s * 0.5f;
            rs.FillAll((x, y) =>
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                return new Color(1f, 1f, 1f, a);
            });
            return rs;
        }

        private static Raster Vignette(int s)
        {
            var rs = new Raster(s, s);
            float c = s * 0.5f;
            rs.FillAll((x, y) =>
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (c * 1.2f);
                float a = Mathf.SmoothStep(0.45f, 1f, d) * 0.85f;
                return new Color(0.04f, 0.06f, 0.11f, a);
            });
            return rs;
        }

        private static Raster WoodGrain(int s)
        {
            var rs = new Raster(s, s);
            var random = new System.Random(7);
            var rowValue = new float[s];
            float v = 0.5f;
            for (int y = 0; y < s; y++)
            {
                if (random.NextDouble() < 0.12)
                {
                    v = (float)random.NextDouble();
                }

                rowValue[y] = v;
            }

            rs.FillAll((x, y) =>
            {
                int iy = Mathf.Clamp((int)y, 0, s - 1);
                float wave = Mathf.Sin(x / s * Mathf.PI * 2f * 3f + iy * 0.35f) * 0.5f + 0.5f;
                float k = rowValue[iy] * 0.7f + wave * 0.3f;
                return k > 0.62f ? new Color(0f, 0f, 0f, (k - 0.62f) * 0.9f) : new Color(1f, 0.85f, 0.65f, (0.62f - k) * 0.18f);
            });
            return rs;
        }

        private static Raster Dot(int s)
        {
            var rs = new Raster(s, s);
            float c = s * 0.5f;
            rs.Fill(Circle(c, c, c - 1f), Outline);
            rs.Fill(Circle(c, c, c - 6f), Color.white);
            return rs;
        }

        // ================================================================ asset writing

        private static void Save(string name, Raster raster, float border, bool tile = false)
        {
            Texture2D texture = raster.ToTexture();
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 512;
            importer.spriteBorder = border > 0f ? new Vector4(border, border, border, border) : Vector4.zero;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
