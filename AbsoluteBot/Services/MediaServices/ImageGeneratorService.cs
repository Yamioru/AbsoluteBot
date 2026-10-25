using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using AbsoluteBot.Services.UtilityServices;
using Newtonsoft.Json.Linq;
using Serilog;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AbsoluteBot.Services.MediaServices;

/// <summary>
///     Сервис для генерации изображений на основе текста.
/// </summary>
public class ImageGeneratorService(HttpClient httpClient, ConfigService configService) : IImageGeneratorService, IAsyncInitializable
{
    private const int MaxImageWidth = 800;
    private const int Padding = 20;
    private const int RegularFontSize = 16;
    private const int BoldFontSize = 16;
    private const string BoldStyle = "bold";
    private const string UnderlineStyle = "underline";
    private const string RegularStyle = "regular";
    private const int BoldItalicFontSize = 14;
    private const string ImageUploadUrl = "https://upload.imagekit.io/api/v1/files/upload";
    private const char BoldMarker = '*';
    private const char UnderlineMarker = '_';
    private static Font? _regularFont;
    private static Font? _boldFont;
    private static Font? _boldItalicFont;
    private static float _lineHeight;
    private static readonly Color TextColor = Color.Black;
    private static readonly Color BackgroundColor = Color.White;

    private static readonly string MarkdownPattern =
        $@"\{BoldMarker}([^\{BoldMarker}]+)\{BoldMarker}|{UnderlineMarker}([^{UnderlineMarker}]+){UnderlineMarker}|([^\{BoldMarker}{UnderlineMarker}]+)";

    private static readonly Regex MarkdownRegex = new(MarkdownPattern);
    private string? _imgApiKey;

    public async Task InitializeAsync()
    {
        _imgApiKey = await configService.GetConfigValueAsync<string>("ImgApiKey").ConfigureAwait(false);
        LoadFonts();
        if (string.IsNullOrEmpty(_imgApiKey)) Log.Warning("Не удалось загрузить api ключ для подключения к сервису ImageGeneratorService.");
    }

    /// <summary>
    ///     Генерирует изображение на основе командного текста и загружает его на сервер ImageKit.
    /// </summary>
    /// <param name="commandsText">Текст команд, который будет преобразован в изображение.</param>
    /// <returns>URL загруженного изображения или null в случае ошибки.</returns>
    public async Task<string?> GenerateCommandsImageAsync(string commandsText)
    {
        try
        {
            if (_regularFont == null || _boldFont == null || _boldItalicFont == null) return null;
            // Создается изображение из текста
            using var image = CreateImageFromText(commandsText, _regularFont, _boldFont, _boldItalicFont);
            using var ms = new MemoryStream();
            await image.SaveAsync(ms, new PngEncoder()).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            var base64Image = Convert.ToBase64String(ms.ToArray());

            // Загрузка изображения и получение URL
            return await UploadImageToImageKitAsync(base64Image, "commands.png").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при генерации изображения команды.");
            return null;
        }
    }

    /// <summary>
    ///     Вычисляет размеры изображения на основе количества строк текста.
    /// </summary>
    /// <param name="lines">Коллекция строк текста.</param>
    /// <param name="regularFont">Обычный шрифт.</param>
    /// <returns>Кортеж, содержащий ширину и высоту изображения.</returns>
    private static (int imageWidth, int imageHeight) CalculateImageDimensions(IReadOnlyCollection<string> lines, Font regularFont)
    {
        var maxLineWidth = lines.Max(line => MeasureTextWidth(line, regularFont));
        var imageWidth = Math.Min((int) Math.Round(maxLineWidth) + Padding * 2, MaxImageWidth);
        var imageHeight = lines.Count * (int) Math.Round(_lineHeight) + Padding * 2;

        return (imageWidth, imageHeight);
    }

    /// <summary>
    ///     Создает изображение на основе текста.
    /// </summary>
    /// <param name="text">Текст для отображения на изображении.</param>
    /// <param name="boldFont">Жирный шрифт.</param>
    /// <param name="boldItalicFont">Жирный курсивный шрифт.</param>
    /// <param name="regularFont">Обычный шрифт.</param>
    /// <returns>Экземпляр изображения.</returns>
    private static Image<Rgba32> CreateImageFromText(string text, Font regularFont, Font boldFont, Font boldItalicFont)
    {
        var lines = SplitAndWrapText(text, regularFont);
        var (imageWidth, imageHeight) = CalculateImageDimensions(lines, regularFont);

        var image = new Image<Rgba32>(imageWidth, imageHeight);
        image.Mutate(ctx => ctx.Fill(BackgroundColor));

        RenderTextOnImage(image, lines, boldFont, boldItalicFont, regularFont);

        return image;
    }

    private static void LoadFonts()
    {
        try
        {
            var fontCollection = new FontCollection();

            // Получение текущего сборочного контекста
            var assembly = Assembly.GetExecutingAssembly();

            // Загрузка шрифта "Roboto" из встроенного ресурса
            using var regularFontStream = assembly.GetManifestResourceStream("AbsoluteBot.Resources.Fonts.Roboto-Regular.ttf");
            using var boldFontStream = assembly.GetManifestResourceStream("AbsoluteBot.Resources.Fonts.Roboto-Bold.ttf");
            using var boldItalicFontStream = assembly.GetManifestResourceStream("AbsoluteBot.Resources.Fonts.Roboto-BoldItalic.ttf");
            if (regularFontStream != null && boldFontStream != null && boldItalicFontStream != null)
            {
                _boldFont = fontCollection.Add(boldFontStream).CreateFont(BoldFontSize, FontStyle.Bold);
                _regularFont = fontCollection.Add(regularFontStream).CreateFont(RegularFontSize, FontStyle.Regular);

                _boldItalicFont = fontCollection.Add(boldItalicFontStream).CreateFont(BoldItalicFontSize, FontStyle.BoldItalic);
            }
            else
            {
                var systemFonts = SystemFonts.Collection;
                var fallbackFont = systemFonts.Families.FirstOrDefault();
                _regularFont = fallbackFont.CreateFont(RegularFontSize);
                _boldFont = fallbackFont.CreateFont(BoldFontSize, FontStyle.Bold);
                _boldItalicFont = fallbackFont.CreateFont(BoldItalicFontSize, FontStyle.Italic);
                Log.Information("Использован fallback шрифт: " + fallbackFont.Name);
            }

            _lineHeight = _regularFont.Size + 5;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось загрузить шрифты из встроенных ресурсов. Используются базовые шрифты по умолчанию.");
        }
    }

    /// <summary>
    ///     Измеряет ширину текста с использованием заданного шрифта.
    /// </summary>
    /// <param name="text">Текст для измерения.</param>
    /// <param name="font">Шрифт для измерения текста.</param>
    /// <returns>Ширина текста в пикселях.</returns>
    private static float MeasureTextWidth(string text, Font font)
    {
        var textOptions = new TextOptions(font);
        var textSize = TextMeasurer.MeasureAdvance(text, textOptions);
        return textSize.Width;
    }

    /// <summary>
    ///     Парсит текст с разметкой Markdown и возвращает список кортежей, содержащих текст и стиль.
    /// </summary>
    /// <param name="text">Текст с разметкой Markdown.</param>
    /// <returns>Список кортежей, содержащих текст и стиль ("bold", "underline" или "regular").</returns>
    private static List<Tuple<string, string>> ParseMarkdown(string text)
    {
        return (from match in MarkdownRegex.Matches(text)
            select match.Value
            into value
            let style = value.StartsWith(BoldMarker) ? BoldStyle : value.StartsWith(UnderlineMarker) ? UnderlineStyle : RegularStyle
            select Tuple.Create(value.Trim(BoldMarker, UnderlineMarker), style)).ToList();
    }

    /// <summary>
    ///     Отображает отформатированный текст на изображении.
    /// </summary>
    /// <param name="image">Изображение для отрисовки текста.</param>
    /// <param name="line">Строка текста.</param>
    /// <param name="xPosition">Позиция по оси X.</param>
    /// <param name="yPosition">Позиция по оси Y.</param>
    /// <param name="textBrush">Кисть для рисования текста.</param>
    /// <param name="boldFont">Жирный шрифт.</param>
    /// <param name="boldItalicFont">Жирный курсивный шрифт.</param>
    /// <param name="regularFont">Обычный шрифт.</param>
    private static void RenderFormattedText(Image<Rgba32> image, string line, float xPosition, float yPosition,
        Brush textBrush, Font boldFont, Font boldItalicFont, Font regularFont)
    {
        var parts = ParseMarkdown(line);

        foreach (var part in parts)
        {
            var font = part.Item2 switch
            {
                BoldStyle => boldFont,
                UnderlineStyle => boldItalicFont,
                _ => regularFont
            };
            var position = xPosition;
            image.Mutate(ctx => ctx.DrawText(part.Item1, font, textBrush, new PointF(position, yPosition)));
            xPosition += MeasureTextWidth(part.Item1, font);
        }
    }

    /// <summary>
    ///     Отображает текст на изображении, разбивая его на несколько строк.
    /// </summary>
    /// <param name="image">Изображение для отрисовки текста.</param>
    /// <param name="lines">Список строк текста.</param>
    /// <param name="boldFont">Жирный шрифт.</param>
    /// <param name="boldItalicFont">Жирный курсивный шрифт.</param>
    /// <param name="regularFont">Обычный шрифт.</param>
    private static void RenderTextOnImage(Image<Rgba32> image, IReadOnlyList<string> lines, Font boldFont, Font boldItalicFont, Font regularFont)
    {
        var textBrush = Brushes.Solid(TextColor);

        for (var i = 0; i < lines.Count; i++)
        {
            var currentY = Padding + i * _lineHeight;
            RenderFormattedText(image, lines[i], Padding, currentY, textBrush, boldFont, boldItalicFont, regularFont);
        }
    }

    /// <summary>
    ///     Разбивает текст на строки и переносит его, если ширина строки превышает максимальную ширину.
    /// </summary>
    /// <param name="text">Текст для разбивки.</param>
    /// <param name="regularFont">Обычный шрифт.</param>
    /// <returns>Список строк текста.</returns>
    private static List<string> SplitAndWrapText(string text, Font regularFont)
    {
        var rawLines = text.Split('\n');
        var lines = new List<string>();

        foreach (var line in rawLines) lines.AddRange(WrapText(line, regularFont, MaxImageWidth - Padding * 2));
        return lines;
    }

    /// <summary>
    ///     Загружает изображение на сервер ImageKit.
    /// </summary>
    /// <param name="base64Image">Base64 строка, представляющая изображение.</param>
    /// <param name="fileName">Имя файла изображения.</param>
    /// <returns>URL загруженного изображения или null в случае ошибки.</returns>
    private async Task<string?> UploadImageToImageKitAsync(string base64Image, string fileName)
    {
        try
        {
            // Настройка заголовков запроса
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(ImageUploadUrl),
                Headers =
                {
                    {"Accept", "application/json"},
                    {"Authorization", $"Basic {_imgApiKey}"}
                },
                Content = new MultipartFormDataContent
                {
                    // Передача base64 строки в параметр file
                    new StringContent(base64Image)
                    {
                        Headers =
                        {
                            ContentDisposition = new ContentDispositionHeaderValue("form-data")
                            {
                                Name = "file"
                            }
                        }
                    },
                    // Передача имени файла
                    new StringContent(fileName)
                    {
                        Headers =
                        {
                            ContentDisposition = new ContentDispositionHeaderValue("form-data")
                            {
                                Name = "fileName"
                            }
                        }
                    }
                }
            };

            // Отправка запроса и получение ответа
            using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            // Парсинг и возврат URL загруженного изображения
            var json = JObject.Parse(body);
            return json["url"]?.ToString();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при загрузке изображения на ImageKit.");
            return null;
        }
    }

    /// <summary>
    ///     Разбивает строку текста на слова и возвращает список строк, которые не превышают указанную ширину.
    /// </summary>
    /// <param name="text">Текст для разбивки на строки.</param>
    /// <param name="font">Шрифт для измерения текста.</param>
    /// <param name="maxWidth">Максимальная ширина строки в пикселях.</param>
    /// <returns>Список строк, не превышающих указанную ширину.</returns>
    private static List<string> WrapText(string text, Font font, int maxWidth)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            var lineWidth = MeasureTextWidth(testLine, font);

            if (lineWidth < maxWidth)
            {
                // Если текущая строка вместе с новым словом помещается в maxWidth, добавляется слово
                currentLine = testLine;
            }
            else
            {
                // Если не помещается, в список добавляется строка и начинается новая строка
                lines.Add(currentLine);
                currentLine = word;
            }
        }

        // Добавляется последняя строка, если она не пустая
        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);

        return lines;
    }
}