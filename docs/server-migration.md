# Перенос AbsoluteBot на новый сервер (MobaXterm)

Папка `data` со старого сервера — это весь рабочий диск бота: `config.json`, cookie VK, истории чата, роли, дни рождения, ключ Google, статистика стрима. Образ Docker и старый git не нужны. Файл `scripts/github.env` в `data` не входит. Без него бот в чатах работает, а команда `!обновить` — нет. Её можно настроить отдельным шагом после запуска.

Команды ниже выполняются в терминале SSH нового сервера в MobaXterm, не в локальном PowerShell. Загрузка `data` — левой панелью SFTP той же сессии.

## 0. До сервера

В Visual Studio закоммитить и запушить текущее дерево: корень репозитория — папка решения, в `docker-compose.yml` лимит **512m / 0.5 CPU**. Пока этого нет на GitHub, клон на новом сервере соберётся по старому Dockerfile и упрётся в память.

На старом сервере, отдельной сессией MobaXterm, остановить бота и больше его не поднимать:

```bash
docker stop absolutebot
systemctl stop absolutebot-update
docker ps
```

`docker ps` не должен показывать `absolutebot`.

## 1. Новый сервер: проверка

```bash
cat /etc/os-release
free -h
swapon --show
wg show
curl -4 --max-time 20 ifconfig.me; echo
```

Нужны Debian или Ubuntu, живой WireGuard и ответ curl. Конфиг WireGuard не менять. Если `free -h` показывает меньше примерно 200 МБ свободно, сначала шаг 2.

## 2. Swap 1 ГБ

Пропустить, если `swapon --show` уже показывает swap.

```bash
fallocate -l 1G /swapfile || dd if=/dev/zero of=/swapfile bs=1M count=1024
chmod 600 /swapfile
mkswap /swapfile
swapon /swapfile
grep -q '/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
free -h
```

## 3. Docker и git

```bash
apt-get update
apt-get install -y ca-certificates curl git
curl -fsSL https://get.docker.com | sh
systemctl enable --now docker
docker --version
docker compose version
docker info >/dev/null && echo 'docker daemon OK'
```

## 4. Клон в /opt/absolutebot

Каталог должен быть пустым. Если `git clone` спросит пароль, это GitHub PAT, не пароль аккаунта.

```bash
rm -rf /opt/absolutebot
git clone https://github.com/Vireslumen/AbsoluteBot.git /opt/absolutebot
cd /opt/absolutebot
ls -la
```

В списке должны быть `.git`, `Dockerfile`, `docker-compose.yml`, `AbsoluteBot`, `scripts`. Проверка лимита:

```bash
grep -E 'mem_limit|cpus' docker-compose.yml
```

Ожидается `512m` и `0.5`. Если там `1000m`, на GitHub ещё старый compose: запушить заново и `git pull`.

Убрать возможный CR и подготовить каталог данных:

```bash
sed -i 's/\r$//' Dockerfile docker-compose.yml scripts/*.sh scripts/*.service
mkdir -p /opt/absolutebot/data
```

## 5. Залить data через SFTP

В левой панели MobaXterm открыть `/opt/absolutebot/data`. Перетащить **содержимое** скачанной папки `data`, не саму папку внутрь `data`.

После загрузки в терминале:

```bash
test -f /opt/absolutebot/data/config.json && echo OK || echo 'НЕТ config.json'
ls /opt/absolutebot/data | head
```

Должно напечатать `OK`. Если файл лежит в `/opt/absolutebot/data/data/config.json`, поднять его на уровень вверх:

```bash
mv /opt/absolutebot/data/data/* /opt/absolutebot/data/
rmdir /opt/absolutebot/data/data
```

Папку `achievement_model`, если она попала в загрузку, удалить:

```bash
rm -rf /opt/absolutebot/data/achievement_model
```

Она не читается, пока ачивки выключены.

## 6. Сборка и старт

```bash
cd /opt/absolutebot
ABS_DATA_DIR=/opt/absolutebot/data docker compose up -d --build
```

Первая сборка долгая. Смотреть, что машина жива: второй вкладкой `free -h`.

Проверка:

```bash
docker ps
docker stats --no-stream
docker logs --tail 80 absolutebot
ss -lntp | grep 5000 || true
```

В логах — подключение платформ и фраза, что ачивки выключены. Контейнер не должен уходить в Restarting. Память в `docker stats` ниже 512 МБ. В чате одна команда, например `!команды`. Старый сервер не запускать.

## 7. Команда !обновить

Нужна только если бот должен сам делать `git pull`. На сервере:

```bash
cp /opt/absolutebot/scripts/github.env.example /opt/absolutebot/scripts/github.env
chmod 600 /opt/absolutebot/scripts/github.env
nano /opt/absolutebot/scripts/github.env
```

В файле две строки, без кавычек и без пробелов вокруг `=`:

```bash
GITHUB_USER=логин
GITHUB_TOKEN=github_pat_...
```

`GITHUB_USER` — это имя пользователя GitHub, не почта и не название токена. Оно видно на странице [github.com/settings/profile](https://github.com/settings/profile) в поле Username. Для этого репозитория владелец в адресе `github.com/Vireslumen/AbsoluteBot` — `Vireslumen`. Если токен выпускает другой аккаунт, у которого есть доступ к репозиторию, в `GITHUB_USER` пишется логин того аккаунта, который создал токен.

`GITHUB_TOKEN` — персональный токен доступа (PAT). Пароль от входа на GitHub сюда не подходит: по HTTPS git принимает именно токен. Создать его в браузере, будучи залогиненным тем же аккаунтом:

1. Открыть [github.com/settings/personal-access-tokens](https://github.com/settings/personal-access-tokens) → **Generate new token** → **Fine-grained token**.
2. Имя любое, например `absolutebot-server`. Срок — на своё усмотрение: когда токен протухнет, `git pull` перестанет проходить, пока в файл не впишут новый.
3. **Resource owner** — владелец репозитория (`Vireslumen`). **Repository access** → **Only select repositories** → `AbsoluteBot`.
4. **Permissions** → **Repository permissions** → **Contents** = **Read-only**. Остальные права не нужны: watcher только скачивает коммиты.
5. **Generate token**. Строка показывается один раз, начинается с `github_pat_`. Её целиком вставить в `GITHUB_TOKEN`.

В nano сохранить: Ctrl+O, Enter, выйти: Ctrl+X. Содержимое файла никуда не копировать: не в чат, не в git, не в скриншот.

Если на старом сервере или на своём компьютере ещё лежит прежний `scripts/github.env`, можно перенести те же две строки. В скачанной папке `data` этого файла нет.

После правки файла watcher нужно перезапустить, иначе он останется со старым окружением:

```bash
systemctl restart absolutebot-update
```

Файл не коммитить.

```bash
cp /opt/absolutebot/scripts/absolutebot-update.service /etc/systemd/system/absolutebot-update.service
systemctl daemon-reload
systemctl enable --now absolutebot-update
systemctl status absolutebot-update --no-pager
```

В unit уже стоит `UPDATE_GIT_DIR=/opt/absolutebot`.

## Если что-то пошло не так

- `Cannot connect to the Docker daemon at unix:///var/run/docker.sock`: демон не запущен. `systemctl enable --now docker`, затем `systemctl status docker --no-pager`. Если статус `failed`, смотреть `journalctl -u docker --no-pager -n 50`.
- Контейнер `Restarting` и в логах OOM: в `docker-compose.yml` поднять `mem_limit` до `640m` и снова `docker compose up -d`. Не возвращать `1000m`.
- Хост постоянно в swap уже после старта бота: опустить лимит до `384m`.
- В логах нет платформ: открыть `data/config.json` на месте и проверить, что загрузка не обрезала файл. Токены в чат и в git не копировать.
