using System.Collections.Concurrent;
using System.Text.Json;
using AbsoluteBot.Services.UtilityServices;
using Serilog;

namespace AbsoluteBot.Services.UserManagementServices;

/// <summary>
/// Сервис для работы с SBTI типами пользователей (27 meme personality types).
/// </summary>
public class SbtiService : IAsyncInitializable
{
    private static string FilePath => DataPaths.Get("sbti_data.json");
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    // База данных описаний для промптов нейросети
    public static readonly IReadOnlyDictionary<string, string> SbtiDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        {"CTRL", "The Puppeteer (Кукловод). Серый кардинал, манипулятор, всегда на шаг впереди."},
        {"ATM-ER", "The ATM (Банкомат). Спонсор команды, решает проблемы деньгами."},
        {"DIOR-S", "The Underdog Sage (Андердог). Начинает на дне, его все бьют, но имеет потенциал для эпичного камбэка."},
        {"BOSS", "The Boss (Босс). Прирожденный лидер, берет руль в свои руки, раздает приказы."},
        {"THAN-K", "The Gratitude Guru (Гуру благодарности). Токсично-позитивный, пацифист, верит в добро."},
        {"OH-NO", "The Oh-No Person (Паникёр). Трусливый комик-релиф, вечно кричит, паникует и плачет."},
        {"GOGO", "The Go-Getter (Энерджайзер). Сначала делает, потом думает. Гиперактивный мотор команды."},
        {"SEXY", "The Stunner (Сногсшибательный). Харизма и внешность — его оружие. Уверен в себе."},
        {"LOVE-R", "The Hopeless Romantic (Симп). Живет ради краша, вся мотивация строится вокруг объекта обожания."},
        {"MUM", "The Mom Friend (Мамочка). Заботится о всей куче идиотов в команде, лечит, отчитывает."},
        {"FAKE", "The Shapeshifter (Двуличный). Носит маски, скрывает пугающую натуру за фальшивой улыбкой."},
        {"OJBK", "The Whatever Person (Пофигист). Плывет по течению. Мир рушится, а ему пофиг."},
        {"MALO", "The Monkey Brain (Обезьяний мозг). Хаотично-глупый, живет инстинктами. Ноль мыслей, абсолютно счастлив."},
        {"JOKE-R", "The Joker (Шут-Нигилист). Скрывает травмы за черным юмором, смеется над абсурдом мира."},
        {"WOC!", "The WTF Person (Адекватный). Единственный адекват в дурдоме, постоянно поражается дичи остальных."},
        {"THIN-K", "The Thinker (Мыслитель). Стратег, зависает в чертогах разума, анализируя каждый шаг."},
        {"SHIT", "The Raging Realist (Бешеный реалист). Циник, видит мир как мусор и не стесняется об этом говорить."},
        {"ZZZZ", "The Playing-Dead Pro (Профессиональный ленивец). Кажется мертвым или спящим. Избегает ответственности."},
        {"POOR", "The Laser-Focused Minimalist (Целеустремленный нищеброд). Бедность сделала его практичным выживальщиком."},
        {"MONK", "The Monk (Монах). Свободен от желаний. Эмоционально отстранен, лицо кирпичом."},
        {"IMSB", "The Self-Doubter (Самозванец). Низкая самооценка, сомневается в себе, извиняется за существование."},
        {"SOLO", "The Lone Wolf (Одиночка-Эджлорд). Трагичное прошлое. Отталкивает людей, чтобы не привязываться."},
        {"FUCK", "The Wild Card (Отбитый). Хаотичный, агрессивный, ходячая бочка с порохом."},
        {"DEAD", "The Flatlined (Мёртвый внутри). Существует по инерции, выгорание 100%."},
        {"IMFW", "The Delicate Flower (Хрупкий цветок). Бесполезен в бою, требует защиты, часто плачет."},
        {"HHHH", "The Giggler (Хихикающий психопат). Смеется в жутких ситуациях, поехавший."},
        {"DRUNK", "The Drunkard (Пьяница/Зависимый). Глушит боль реальности зависимостями, скрывает жизненную мудрость."}
    };

    private ConcurrentDictionary<string, string> _sbtiData = new();

    public async Task InitializeAsync()
    {
        await LoadSbtiAsync().ConfigureAwait(false);
    }

    public static string GetSbtiDescription(string sbti)
    {
        var key = sbti.Trim().ToUpper();
        return SbtiDescriptions.TryGetValue(key, out var desc) ? desc : "Неизвестный архетип.";
    }

    public virtual string? GetSbtiForUser(string username)
    {
        return _sbtiData.TryGetValue(username.ToLower(), out var sbti) ? sbti : null;
    }

    public static bool IsValidSbti(string sbti)
    {
        return SbtiDescriptions.ContainsKey(sbti.Trim().ToUpper());
    }

    public virtual async Task<bool> SetSbtiForUserAsync(string username, string sbti)
    {
        try
        {
            _sbtiData[username.ToLower()] = sbti.Trim().ToUpper();
            await SaveDataAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при добавлении SBTI для пользователя.");
            return false;
        }
    }

    private async Task LoadSbtiAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (File.Exists(FilePath))
            {
                var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
                _sbtiData = JsonSerializer.Deserialize<ConcurrentDictionary<string, string>>(json) ?? new ConcurrentDictionary<string, string>();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при загрузке SBTI данных.");
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private async Task SaveDataAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(_sbtiData, JsonOptions);
            await File.WriteAllTextAsync(FilePath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при сохранении SBTI данных.");
        }
        finally
        {
            Semaphore.Release();
        }
    }
}