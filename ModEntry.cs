using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace HostControls
{
    public class HostMessage
    {
        public string Action { get; set; } = "";
        public bool IsPaused { get; set; }
    }

    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
        void AddKeybind(IManifest mod, Func<SButton> getValue, Action<SButton> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);
    }

    public class ModEntry : Mod
    {
        public static ModConfig Config { get; private set; } = new ModConfig();

        // Pause state
        public static bool IsForcedPaused { get; private set; } = false;

        // Custom buttons texture (120x60: left 60x60 is Play, right 60x60 is Pause)
        private static Texture2D? _buttonsTexture;
        private static readonly Rectangle PlaySourceRect = new(0, 0, 60, 60);
        private static readonly Rectangle PauseSourceRect = new(60, 0, 60, 60);

        // UI button bounds
        private static Rectangle _buttonRect;

        private const string MessageType = "LegoManchik.HostControls.Sync";

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();

            try
            {
                _buttonsTexture = helper.ModContent.Load<Texture2D>("buttons.png");
            }
            catch (Exception ex)
            {
                Monitor.Log($"Ошибка загрузки иконки buttons.png: {ex.Message}", LogLevel.Error);
            }

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Display.Rendered += OnRendered;
            helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu != null)
            {
                configMenu.Register(
                    mod: ModManifest,
                    reset: () => Config = new ModConfig(),
                    save: () => Helper.WriteConfig(Config)
                );

                configMenu.AddSectionTitle(ModManifest, () => "Управление паузой хоста");

                configMenu.AddBoolOption(
                    mod: ModManifest,
                    getValue: () => Config.EnableOnScreenButton,
                    setValue: val => Config.EnableOnScreenButton = val,
                    name: () => "Кнопка на экране",
                    tooltip: () => "Отображать кнопку паузы справа возле часов (только для хоста)."
                );

                configMenu.AddKeybind(
                    mod: ModManifest,
                    getValue: () => Config.PauseHotkey,
                    setValue: val => Config.PauseHotkey = val,
                    name: () => "Клавиша паузы",
                    tooltip: () => "Горячая клавиша для принудительной паузы всего мира (по умолчанию F10)."
                );

                configMenu.AddBoolOption(
                    mod: ModManifest,
                    getValue: () => Config.DarkenScreenOnPause,
                    setValue: val => Config.DarkenScreenOnPause = val,
                    name: () => "Затемнять экран при паузе",
                    tooltip: () => "Мягкое затемнение экрана во время паузы без лишнего текста."
                );

                configMenu.AddBoolOption(
                    mod: ModManifest,
                    getValue: () => Config.InvertButtonIcons,
                    setValue: val => Config.InvertButtonIcons = val,
                    name: () => "Инвертировать спрайт",
                    tooltip: () => "По умолчанию: значок || когда игра идет, значок ▶ когда на паузе."
                );

                configMenu.AddSectionTitle(ModManifest, () => "Расположение кнопки (относительно часов)");

                configMenu.AddNumberOption(
                    mod: ModManifest,
                    getValue: () => Config.OffsetX,
                    setValue: val => Config.OffsetX = val,
                    name: () => "Смещение X",
                    tooltip: () => "Сдвиг кнопки по горизонтали относительно часов.",
                    min: -200,
                    max: 200,
                    interval: 2
                );

                configMenu.AddNumberOption(
                    mod: ModManifest,
                    getValue: () => Config.OffsetY,
                    setValue: val => Config.OffsetY = val,
                    name: () => "Смещение Y",
                    tooltip: () => "Сдвиг кнопки по вертикали относительно часов.",
                    min: -100,
                    max: 200,
                    interval: 2
                );

                configMenu.AddNumberOption(
                    mod: ModManifest,
                    getValue: () => Config.ButtonSize,
                    setValue: val => Config.ButtonSize = val,
                    name: () => "Размер кнопки",
                    min: 32,
                    max: 96,
                    interval: 2
                );
            }
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            IsForcedPaused = false;
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (Context.IsMainPlayer)
            {
                // Hotkey toggle
                if (e.Button == Config.PauseHotkey)
                {
                    Game1.playSound("drumkit6");
                    SetPause(!IsForcedPaused);
                    return;
                }

                // Check button click on screen
                if (Config.EnableOnScreenButton && e.Button == SButton.MouseLeft)
                {
                    Point mousePos = new Point(Game1.getMouseX(), Game1.getMouseY());
                    if (_buttonRect.Contains(mousePos))
                    {
                        Game1.playSound("drumkit6");
                        SetPause(!IsForcedPaused);
                        Helper.Input.Suppress(SButton.MouseLeft);
                    }
                }
            }
        }

        public void SetPause(bool pause)
        {
            if (!Context.IsMainPlayer)
                return;

            IsForcedPaused = pause;

            try
            {
                Game1.netWorldState.Value.IsPaused = IsForcedPaused;
            }
            catch { }

            Game1.freezeControls = IsForcedPaused;

            // Broadcast to all clients
            Helper.Multiplayer.SendMessage(
                new HostMessage { Action = "PauseToggle", IsPaused = IsForcedPaused },
                MessageType,
                modIDs: new[] { ModManifest.UniqueID }
            );

            string logMsg = IsForcedPaused ? "Игра поставлена на паузу хостом." : "Игра возобновлена хостом.";
            Monitor.Log(logMsg, LogLevel.Info);
        }

        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != ModManifest.UniqueID || e.Type != MessageType)
                return;

            var msg = e.ReadAs<HostMessage>();
            if (msg == null)
                return;

            if (msg.Action == "PauseToggle")
            {
                IsForcedPaused = msg.IsPaused;
                Game1.freezeControls = IsForcedPaused;
            }
        }

        private void OnRendered(object? sender, RenderedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            var b = e.SpriteBatch;
            int screenWidth = Game1.uiViewport.Width;
            int screenHeight = Game1.uiViewport.Height;
            Point mousePos = new Point(Game1.getMouseX(), Game1.getMouseY());

            // 1. Fullscreen darkening during Pause (drawn first, so button and cursor are on top)
            if (IsForcedPaused && Config.DarkenScreenOnPause)
            {
                float alpha = Context.IsMainPlayer ? 0.35f : 0.45f;
                b.Draw(
                    Game1.fadeToBlackRect,
                    new Rectangle(0, 0, screenWidth, screenHeight),
                    Color.Black * alpha
                );
            }

            bool isHover = false;

            // 2. Draw Single Host Pause Button anchored to the Clock on the right (ONLY for the Host!)
            // Keep button visible during cutscenes (eventUp), screen fades (fadeToBlack/isFading), paused state, dialogue box, or when no blocking menu is open
            bool isFading = Game1.globalFade || Game1.fadeToBlack || Game1.fadeToBlackAlpha > 0f;
            bool canShowButton = Context.IsMainPlayer && Config.EnableOnScreenButton &&
                (Game1.activeClickableMenu == null || Game1.eventUp || isFading || IsForcedPaused || Game1.activeClickableMenu is StardewValley.Menus.DialogueBox);

            if (canShowButton)
            {
                int btnSize = Config.ButtonSize;

                // Base coordinates aligned with the Clock widget (DayTimeMoneyBox)
                int baseX;
                int baseY;
                if (Game1.dayTimeMoneyBox != null)
                {
                    baseX = (int)Game1.dayTimeMoneyBox.position.X - btnSize - 12;
                    baseY = (int)Game1.dayTimeMoneyBox.position.Y + 12;
                }
                else
                {
                    baseX = screenWidth - 268 - btnSize - 12;
                    baseY = 20;
                }

                int btnX = baseX + Config.OffsetX;
                int btnY = baseY + Config.OffsetY;

                _buttonRect = new Rectangle(btnX, btnY, btnSize, btnSize);

                isHover = _buttonRect.Contains(mousePos);

                if (_buttonsTexture != null)
                {
                    // Select sprite:
                    // When running: show Pause (||) so clicking it pauses
                    // When paused:  show Play (▶) so clicking it resumes
                    Rectangle sourceRect;
                    if (!Config.InvertButtonIcons)
                    {
                        sourceRect = IsForcedPaused ? PlaySourceRect : PauseSourceRect;
                    }
                    else
                    {
                        sourceRect = IsForcedPaused ? PauseSourceRect : PlaySourceRect;
                    }

                    Color tint = isHover ? new Color(255, 235, 180) : Color.White;

                    b.Draw(_buttonsTexture, _buttonRect, sourceRect, tint);
                }
            }
            else
            {
                _buttonRect = Rectangle.Empty;
            }

            // 3. Ensure mouse cursor is drawn if paused or hovering over button (vanilla hides it during eventUp or freezeControls)
            if (IsForcedPaused || (canShowButton && isHover))
            {
                Game1.wasMouseVisibleThisFrame = true;

                if (!Game1.options.hardwareCursor)
                {
                    int cursor = isHover ? 44 : (Game1.mouseCursor > 0 ? Game1.mouseCursor : Game1.cursor_default);
                    float scale = 4f + Game1.dialogueButtonScale / 150f;
                    Vector2 mousePosVec = new Vector2(Game1.getMouseX(), Game1.getMouseY());
                    Rectangle srcRect = Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, cursor, 16, 16);

                    b.Draw(
                        Game1.mouseCursors,
                        mousePosVec,
                        srcRect,
                        Color.White,
                        0f,
                        Vector2.Zero,
                        scale,
                        SpriteEffects.None,
                        1f
                    );
                }
            }
        }
    }
}
