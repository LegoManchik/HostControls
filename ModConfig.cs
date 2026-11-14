using StardewModdingAPI;

namespace HostControls
{
    public class ModConfig
    {
        public bool EnableOnScreenButton { get; set; } = true;
        public int OffsetX { get; set; } = 0; // Смещение по горизонтали относительно часов
        public int OffsetY { get; set; } = 0; // Смещение по вертикали относительно часов
        public int ButtonSize { get; set; } = 60; // Размер кнопки (60x60)
        public SButton PauseHotkey { get; set; } = SButton.F10;
        public bool InvertButtonIcons { get; set; } = false; // Инвертировать спрайты (Play/Pause)
        public bool DarkenScreenOnPause { get; set; } = true; // Легкое затемнение экрана во время паузы
    }
}
