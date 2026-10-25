# AbsoluteBot — архитектура

## Назначение

Кросс-платформенный чат-бот на .NET 8 для Twitch, VkPlayLive, Telegram и Discord: команды, модерация, нейросеть (Gemini/Groq/GPT), Google Sheets, расписания, endpoint Яндекс.Алисы (`POST /Sobeka`).

Каталог команд и описание для показа: [README.md](README.md).

## Слои

```
Program
  → ChatBot (IAsyncInitializable → IChatService.Connect)
  → IChatCommand / CommandRegistry / CommandExecutionService
  → Services (Media, Neural, Google, UserManagement, Utility, Scheduled, ChatServices)
  → Models / Chat.Context
```

- **Chat / Commands** — парсинг и выполнение команд (`BaseCommand`, User/Admin).
- **Services.ChatServices** — адаптеры платформ (handlers, processors). **Не вызывать Connect в тестах** при живом боте на VDS (конфликт Telegram getUpdates и дубли ответов).
- **Services.*** — бизнес-логика и внешние API.
- **BeginData / data-root** — JSON-конфиги и данные.

## Корень данных

| Среда | Корень |
|---|---|
| Локальный запуск бота | Обычно CWD = `BeginData/` |
| Docker | volume `./data` → `/app` |
| Тесты | `ABSOLUTEBOT_DATA_ROOT` → temp-копия BeginData |

Разрешение путей: `AbsoluteBot.Services.UtilityServices.DataPaths` (`DataPaths.Get("config.json")`).

Генерируемые отчёты стрима: `stream_stats/stream_{N}.json` (относительно data-root). Тихий прогресс ачивок: `achievements/progress.json`.

SBTI и каталоги персонажей: `sbti_data.json`, `sbti_characters.json`, `mbti_data.json`, `mbti_characters.json`. Персональные JSON в публикацию репозитория не входят, список — в [README.md](README.md).

Модели нейросети: `neural_models.json` (провайдер `gemini`/`groq` и ID моделей по сущностям Chat/Ask/GoogleSearch/Image). Смена из админ-канала без перезапуска. В Groq уходит урезанная история (первые 2 + последние 30 сообщений); для `gpt-oss` ставится `reasoning_effort=low`, иначе reasoning съедает дневной лимит токенов.

Сессия VK Live: cookie `auth` в `VkPlayAuthToken` (JSON с `accessToken`, `refreshToken`, `expiresAt`) и Bearer `VkPlayAuthSendToken`. Первый раз cookie копируется из браузера (`!setconfig VkPlayAuthToken ...`); дальше `VkPlayAuthService` обновляет её через `POST https://api.live.vkvideo.ru/oauth/token/` (`grant_type=refresh_token`, `device_id` из `_clientId` в `vkplay_request_cookies.txt` или ключа `VkPlayClientId`). WebSocket-токен — `GET /v1/ws/connect`. Если refresh окончательно не удался, в административный канал Telegram уходит уведомление (кулдаун 1 час). Шаблон куки `vkplay_request_cookies.txt` (плейсхолдер `{auth}`) нужен для `_clientId`; файл не в git, образец — `vkplay_request_cookies.txt.example`. Запасной вариант шаблона: ключ `VkPlayRequestCookies` в `config.json`.

Переменная: `ABSOLUTEBOT_DATA_ROOT`. Если не задана или путь не существует — используется `Directory.GetCurrentDirectory()`.

## Dual-instance

Одновременный локальный бот + VDS с теми же токенами ломает Telegram long polling и дублирует сообщения на остальных платформах. Тесты **никогда** не вызывают `IChatService.Connect` / Login к прод-аккаунтам; используют `FakeChatService`.

## Тесты

Проект `AbsoluteBotTests` (xUnit + Moq) лежит в этом репозитории. Команды ниже запускаются из корня решения.

| Trait | Назначение |
|---|---|
| `Category=Unit` | Без сети / MockHttp |
| `Category=Integration` | Живая сеть |
| `RequiresVpn=true` | Нужен VPN (гео) |
| `LiveAi=true` | Редкие Gemini/GPT; ещё `RUN_LIVE_AI_TESTS=1` |
| `LiveSheets=true` | Нужен `TEST_GOOGLE_SHEETS_ID` |
| `Area=...` | Foundation / Commands / Services / Handlers |

### Visual Studio

1. Test → Configure Run Settings → Select Solution Wide runsettings → `AbsoluteBotTests/unit.runsettings`
2. Test Explorer → Run All (безопасный набор без VPN)
3. Для VPN: `integration.vpn.runsettings` или фильтр по Trait `RequiresVpn`

### Структура тестов

| Папка | Содержание |
|---|---|
| `Foundation/` | Config, roles, censorship, command pipeline |
| `Commands/User/`, `Commands/Admin/` | Команды без Connect |
| `Services/`, `Services/Media/`, `Services/Neural/`, `Services/TextChat/`, `Services/Scheduled/` | Unit сервисов (BeginData / MockHttp) |
| `Handlers/` | BaseMessageHandler, MessageDataProcessor без Connect |
| `Integration/` | Live AI (`RUN_LIVE_AI_TESTS=1`), эмбеддинги ачивок (`OnnxEmbeddingAchievementLiveTests`, Hugging Face), Google Sheets (`TEST_GOOGLE_SHEETS_ID`) |
| корень `AbsoluteBotTests/` | Legacy live media (Image/Video/HLTB/…) с `RequiresVpn` |

- Traits: `Category=Unit|Integration`, `Area=Foundation|Commands|Services|Handlers`
- Инфраструктура: `BeginDataFixture`, `MockHttpMessageHandler`, `FakeChatService` / `FakeDeletingChatService`, `ChatContextFactory`
- **Не вызывать** `IChatService.Connect` / Login платформ
- `unit.runsettings` исключает `RequiresVpn`, `LiveAi`, `LiveSheets`, `Category=Integration`

### CLI

```bash
dotnet test AbsoluteBot.sln --settings AbsoluteBotTests/unit.runsettings
dotnet test AbsoluteBot.sln --filter FullyQualifiedName~OnnxEmbeddingAchievementLiveTests
dotnet test AbsoluteBot.sln --settings AbsoluteBotTests/integration.vpn.runsettings
```

### VPN Allow (split tunnel)

Чтобы live-тесты ходили через туннель: `dotnet`, `testhost`, `vstest.console` (при запуске из VS при необходимости `devenv`; для Cursor — уже часто есть `cursor`).

### Жизненный цикл процесса

`IProcessController` / `ProcessController` — Restart/Shutdown без прямого `Environment.Exit` в командах (тестируемо через Moq). Команда `!перезагрузка` в Docker только завершает процесс (`Environment.Exit(0)`): у контейнера `restart: always`, второй `dotnet` внутри того же контейнера не запускается. Перед выходом пауза ~1.5 с, чтобы ответ успел уйти в чат. Локально по-прежнему стартует новый процесс `dotnet` и затем Exit.

Команда `!обновить` (`UpdateBotCommand`, только администратор) не вызывает `git`/`docker` из контейнера: пишет флаг `update.requested` в data-root (Docker: volume `./data`). Скрипты watcher лежат в репозитории: [scripts/watch-update.sh](scripts/watch-update.sh) и [scripts/absolutebot-update.service](scripts/absolutebot-update.service). На хосте корень репозитория и compose — `/opt/absolutebot`, проект — каталог `AbsoluteBot/` внутри него. Watcher пуллит git там, где лежит `.git`: если репозиторий поднят на уровень решения, это `/opt/absolutebot`, а не вложенный `AbsoluteBot`. Unit копируется в `/etc/systemd/system/`; `ExecStart` указывает на `/opt/absolutebot/scripts/watch-update.sh`. Watcher делает `git pull --ff-only`, копирует `BeginData/achievements.json` в volume `./data/achievements.json` и собирает образ из каталога с `Dockerfile`, потому что тот делает `COPY AbsoluteBot/`. Хостовый `docker-compose.yml` из git не перезаписывается. Compose/`Dockerfile`/`*.sh` с **LF**; watcher перед сборкой делает `sed` от `\r`. Если текущий контейнер Alpine — `docker compose build --no-cache` (ONNX нужен glibc). Volume данных: `ABS_DATA_DIR` → `/opt/absolutebot/data`. После смены `watch-update.sh` он копируется в `/opt/absolutebot/scripts/` при маркере `compose_host_build`, затем `systemctl restart absolutebot-update`. Логин и PAT GitHub не хранятся в git: либо `/root/.git-credentials` (`credential.helper store`), либо хост-файл `/opt/absolutebot/scripts/github.env` (`GITHUB_USER` / `GITHUB_TOKEN`, `chmod 600`) вместе с [scripts/git-askpass.sh](scripts/git-askpass.sh). Образец — [scripts/github.env.example](scripts/github.env.example).

## Ключевые сервисы (кратко)

- **UserIdentityService** — индекс `user_identities.json`: на платформе человек определяется **id**, ники — алиасы. После смены ника MBTI/SBTI, роли, ДР, автоперевод, статистика стрима и ачивки продолжают относиться к той же личности. Старые JSON с ключом-ником читаются как раньше; id дописывается, когда пользователь пишет в чат.
- **ConfigService**, **RoleService**, **CooldownService**, **CensorshipService** — конфиг и модерация
- **CommandExecutionService** — pipeline команд
- **INeuralAskService / INeuralChatService** — надстройка над Gemini и Groq. Команды и сервисы зависят только от интерфейсов; `NeuralAskService` / `NeuralChatService` выбирают провайдера по `neural_models.json`. Клиенты: `GeminiAskClient`, `GeminiChatClient`, `GroqChatService`. Команда `!нарисуй` генерирует и правит картинки через Cloudflare Workers AI FLUX.2 klein-4b (`CloudflareFluxImageService`, ключи `CloudFlareApiKey` / `CloudFlareAccountId`). `GeminiImageGenerationService` сохранён как устаревший запасной путь (конфиг Image в `neural_models.json` / `!setmodel Image`), но командой не используется. `!сгенерировать` по-прежнему Flux 1 schnell (`ImageGenerationService`). `GeminiSettingsProvider` и `GroqChatService` используют **свои** `HttpClient`. Ключ Groq: `GroqApiKey`. Админ-канал: `!switchai`, `!showmodels`, `!setmodel`. `!спросить` ходит в OpenAI через `ChatGptService` (свой HttpClient). Свободный чат и `!!спросить`, в том числе с картинкой, идут через `INeuralChatService` / `INeuralAskService` и текущий провайдер Gemini или Groq.
- **GoogleSheets*** — учёт игр/стримов (live-тесты только на test spreadsheet)
- **ScheduledTaskService** — периодические задачи без Connect в unit-тестах
- **StreamChatterStatsService** — счётчик сообщений чаттеров за сессию стрима (Twitch + VkPlayLive). Сессия = `StreamNumber`: начинается при старте Twitch-стрима и **не** сбрасывается, когда Twitch заканчивается (VK Live может идти дальше). Новая сессия — только при следующем старте Twitch. Файлы: `stream_stats/stream_{N}.json` (`nickname` + `count`, UTF-8, `UnsafeRelaxedJsonEscaping` — кириллица без `\u`-экранирования). При старте существующий файл перезаписывается тем же энкодером. Счётчики склеиваются по id, если чаттер сменил ник.
- **ChatAchievementService** — **сейчас выключено** (`Enabled = false`): при старте бота не грузятся каталог, e5-модель и не пишется `progress.json`. Код и тесты на месте; чтобы включить — `ChatAchievementService.Enabled = true`. Тихий тестовый учёт ачивок по сообщениям Twitch, VK Live и Telegram. Каталог: `achievements.json` в data-root (30 ачивок; на деплое копируется из `BeginData/` в volume `./data`). Прогресс: `achievements/progress.json` (папка `achievements/` появляется после первой засчитанной ачивки, кириллица без `\u`-экранирования). Классификация **эмбеддингами** (`OnnxEmbeddingAchievementClassifier`, `intfloat/multilingual-e5-small` ONNX INT8 ~118 МБ, без NLI MiniLM, без ключевых слов, без Gemini/Groq/Ollama). Сообщение кодируется как `query: …`, подписи каталога — `passage: …` (кэш векторов). Зачёт только одной ачивки: сначала отсев по словам подписи (целое слово, «кот» не матчит «который»), затем лучший cosine ≥ 0.82, отрыв от второй упомянутой ≥ 0.02, выше decoy («привет» / оскорбление по имени) на ≥ 0.02, `negativeHypothesis` не ближе на ≥ 0.02. Поэтому «котик» не шиппер, «Паша ты чорт» не котики/псы/хоррор. Подпись — поле `nliHypothesis`; если пусто — первое предложение `criteria`. Человекочитаемый `criteria` остаётся в каталоге. Токенайзер XLM-R: SentencePiece + fairseq-сдвиг (`0→unk 3`, иначе `id+1`) и ведущий пробел; mean-pooling + L2. Модель в `achievement_model/e5-small/` (Hugging Face `Xenova/multilingual-e5-small` + `sentencepiece.bpe.model` из e5-small), скачивается в фоне при первом старте. Живые проверки: `OnnxEmbeddingAchievementLiveTests` (`Category=Integration`, сеть на Hugging Face; не входят в `unit.runsettings`). Контейнер: **Debian slim, 1000 МБ, 0.75 CPU** (`libgomp1`, glibc — Alpine не подходит для ONNX). На VIEs-1 одно ядро: 0.75 боту (эмбеддинг однопоточный), ~0.25 хосту (VPN + ядро + Docker). В чат ничего не пишется. Пропуски: `!команды`, пустой текст, роли Ignored/Bot. Кулдауна нет: каждое подходящее сообщение даёт балл. Discord не участвует. Ежедневный пост про праздник в Telegram: короткий факт (до ~450 символов).
- **TelegramChatService** — исходящий текст: целый Markdown (`**`, `##`, `` ` ``, `*жирный*`, `_курсив_`) уходит как HTML; сломанная разметка или отказ Telegram — обычный текст, сообщение не теряется.
- **VkPlayAuthService** — сессия VK Live: refresh через OAuth, WebSocket-токен `/v1/ws/connect`, уведомление в админ-канал Telegram при окончательном сбое.

### Пересылка Twitch → VK Live

`TwitchMessageHandler` шлёт в VK Live одно сообщение вида `[Twitch] DisplayName: text`. Повторная обработка одного IRC-сообщения отсекается дедупом по `ChatMessage.Id`; при reconnect предыдущий Twitch-клиент отключается, чтобы не держать два подключения. Сообщения самого бота на Twitch и VK Live не парсятся и не исполняются как команды. Dual-instance (локальный бот + VDS) по-прежнему может дублировать сообщения.

WebSocket VK Live: Centrifugo `wss://pubsub.live.vkvideo.ru/...&cf_protocol_version=v2`. После `connect` бот подписывается только на **`channel-chat:{VkPlayChannelId}`**. `public-chat:` на сайте нет. `channel-chat:{id}@0` дублирует те же сообщения — не подписываемся. Входящие чат-события: `type: message_v8` (`data.chatMessageSend.message`: `id` строкой, автор `nick`, плоский `text`, `textData` с `BLOCK_END`). Старый `type: message` с `data[]` тоже разбирается. `ClientWebSocket` после Close создаётся заново. В логах: connect/subscribe ACK, ошибки Centrifugo, каждое разобранное чат-сообщение; ping `{}` не пишется.
