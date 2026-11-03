using UnityEngine;

namespace Parkology
{
    public static class Palette
    {
        static Color Hex(uint rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        /// <summary>Block colours, in the order levels introduce them.</summary>
        public static readonly Color[] Blocks =
        {
            Hex(0xF2474D), // red
            Hex(0x3F8CFF), // blue
            Hex(0xFFC83A), // yellow
            Hex(0x3DCB6C), // green
            Hex(0x9A5CF5), // purple
            Hex(0xFF9135), // orange
            Hex(0xFF6EB4), // pink
            Hex(0x23C4C4), // teal
        };

        public static readonly Color Mystery = Hex(0xB9AEE6);
        public static readonly Color Ink = Hex(0x1F2550);
        public static readonly Color Ground = Hex(0x8EDCC8);
        public static readonly Color GroundStud = Hex(0xA4E6D5);
        public static readonly Color Road = Hex(0x3A4170);
        public static readonly Color RoadEdge = Hex(0x2B3159);
        public static readonly Color RoadLine = Hex(0xF4F1E8);
        public static readonly Color Lot = Hex(0xF4F1E8);
        public static readonly Color LotLine = Hex(0xD9D4C6);
        public static readonly Color Wheel = Hex(0x2B3159);
        public static readonly Color Hub = Hex(0xC9D2EA);
        public static readonly Color Cream = Hex(0xFFF7EA);
        public static readonly Color SkyTop = Hex(0x6FD6FF);
        public static readonly Color SkyBottom = Hex(0x4867F0);
        public static readonly Color Gold = Hex(0xFFC83A);
        public static readonly Color Danger = Hex(0xF23B4A);
        public static readonly Color Crate = Hex(0x7F86F0);
        public static readonly Color CrateRib = Hex(0xA9B0FF);
        public static readonly Color CrateEnd = Hex(0x5A5FD0);
        public static readonly Color Sign = Hex(0x3A4170);

        public static Color Block(int index) => index < 0 ? Mystery : Blocks[index % Blocks.Length];
    }
}
