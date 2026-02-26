<p align="center">
  <svg width="64" height="64" viewBox="0 0 64 64" fill="none" xmlns="http://www.w3.org/2000/svg">
    <rect width="64" height="64" rx="16" fill="#111113"/>
    <rect x="12" y="16" width="40" height="26" rx="3" stroke="#F4F4F5" stroke-width="4"/>
    <path d="M32 42V50M24 50H40" stroke="#F4F4F5" stroke-width="4" stroke-linecap="round"/>
    <path d="M18 31H24L28 23L34 37L38 31H46" stroke="#F4F4F5" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>
  </svg>

  <h1 align="center"> Monitorovací systém — Klientský agent</h1>
  <p align="center">
    Nenáročný linuxový daemon pro sběr a odesílání systémových metrik v reálném čase.
    <br />
    Součást projektu <strong>Aplikace pro sběr, monitoring a vizualizaci dat počítačů a serverů</strong>.
  </p>
</p>

<p align="center">
  <a href="README.md">ᴇɴ English Version</a>
</p>

---

## O projektu

**Klientský agent monitorovacího systému** je .NET 9 Worker Service navržený pro provoz jako daemon na pozadí na linuxových strojích. Průběžně sbírá systémové metriky a odesílá je na centrální backend server (Spring Boot), kde se zobrazují v reálném čase jako grafy na webovém dashboardu (Vite + React).

Agent pracuje ve dvou režimech:

| Režim | Popis |
|---|---|
| **CLI režim** | Interaktivní příkazy pro počáteční nastavení zařízení, registraci a správu konfigurace. |
| **Režim daemona** | Služba na pozadí, která periodicky sbírá a odesílá systémové metriky prostřednictvím REST API. |

### Sbírané metriky

| Metrika | Zdroj |
|---|---|
| Využití CPU (%) | `/proc/stat` |
| Frekvence CPU (MHz) | `/proc/cpuinfo` |
| Teplota CPU (°C) | `/sys/class/thermal/` |
| Využití RAM (MB) | `/proc/meminfo` |
| Využití disku (%) | Kořenový souborový systém |
| Síťový provoz In/Out (kbps) | `/proc/net/dev` |
| Počet procesů | Seznam systémových procesů |
| TCP spojení | Aktivní TCP spojení |
| Naslouchající porty | TCP listenery |
| Doba běhu systému (s) | `/proc/uptime` |

---

## Funkce
- **Automatická detekce zařízení** — IP adresa, MAC adresa, model hardware, OS a stav SSH jsou detekovány při registraci
- **Flexibilní konfigurace** — CLI parametry, TOML konfigurační soubor, nebo kombinace obojího
- **Podpora self-signed certifikátů** — připojení k backend serverům s vlastními SSL certifikáty
- **Integrace se systemd** — nativní logování do `journald` a správný životní cyklus služby
- **Konfigurovatelný interval sběru** — nastavení frekvence vzorkování a odesílání metrik

---

## Rychlý start

### Předpoklady

- Linuxový stroj (x64 nebo ARM64)
- Účet na backend serveru monitorovacího systému
- Síťový přístup k backend API

### 1. Stažení

Stáhněte nejnovější binární soubor ze stránky [Releases](../../releases) pro vaši architekturu.

```bash
# Nastavit práva pro spuštění
chmod +x monitoring-agent
```

### 2. Konfigurace agenta

#### Varianta A: Zkopírování příkazu z webového dashboardu (Nejjednodušší)

Při vytvoření nového zařízení nebo regeneraci API klíče ve webové aplikaci se zobrazí připravený **příkaz pro nastavení**. Stačí ho zkopírovat a vložit do terminálu:

```bash
./monitoring-agent setup \
  --server-url=https://vas-server.com \
  --device-id=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx \
  --api-key=vygenerovany-api-klic
```

> **Poznámka:** Název binárního souboru v příkazu se může lišit od staženého souboru. Ujistěte se, že se v terminálu nacházíte ve složce, do které jste binární soubor stáhli nebo nainstalovali (např. pomocí příkazu `cd /opt/monitoring-agent`).

Hotovo — konfigurace je uložena a agent je připraven ke spuštění.

#### Varianta B: Registrace nového zařízení přes CLI

Pokud chcete zařízení zaregistrovat přímo z terminálu (automaticky detekuje informace o hardware):

```bash
./monitoring-agent register \
  --server-url=https://vas-server.com \
  --email=vas@email.com \
  --password=vaseHeslo \
  --device-name="Můj Server"
```

#### Varianta C: Nastavení pomocí přihlašovacích údajů (existující zařízení)

Regeneruje API klíč pro již zaregistrované zařízení pomocí přihlašovacích údajů:

```bash
./monitoring-agent setup \
  --server-url=https://vas-server.com \
  --email=vas@email.com \
  --password=vaseHeslo \
  --device-id=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```

### 3. Ověření konfigurace

```bash
./monitoring-agent print-config
```

---

## Konfigurace

Po nastavení agent ukládá konfiguraci do souboru `agent_config.toml` v pracovním adresáři:

```toml
# Konfigurace monitorovacího agenta

base_url = "https://vas-server.com"
device_id = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
api_key = "vas-api-klic"
interval_seconds = 10
allow_self_signed_certificates = true
```

### Konfigurační parametry

| Klíč | CLI parametr | Výchozí hodnota | Popis |
|---|---|---|---|
| `base_url` | `--server-url` | `https://localhost:8080` | URL backend serveru |
| `device_id` | `--device-id` | — | UUID zařízení přidělené při registraci |
| `api_key` | `--api-key` | — | API klíč pro autentizaci odesílání metrik |
| `interval_seconds` | `--interval` | `10` | Interval sběru metrik v sekundách |
| `allow_self_signed_certificates` | `--allow-self-signed-certificates` | `true` | Povolit self-signed SSL certifikáty |

---

## Přehled CLI příkazů

```
Monitoring System Client Service

POUŽITÍ:
  <příkaz> [parametry]

PŘÍKAZY:
  setup          Inicializace konfigurace klienta
  register       Registrace nového zařízení
  print-config   Zobrazení aktuální konfigurace
  help           Zobrazení nápovědy

VOLITELNÉ PARAMETRY:
  --interval=<sekundy>                           Interval sběru metrik (výchozí: 10).
  --allow-self-signed-certificates=<true|false>  Povolit self-signed SSL certifikáty (výchozí: true).
```

---

## Nastavení systemd daemona

### Varianta A: Automatická instalace

Pro usnadnění je přiložen instalační skript:

```bash
sudo bash install.sh
```

Skript zkopíruje binární soubor do `/opt/monitoring-agent/`, nainstaluje systemd službu a spustí daemona.

### Varianta B: Manuální instalace

#### 1. Zkopírování binárního souboru

```bash
sudo mkdir -p /opt/monitoring-agent
sudo cp monitoring-agent /opt/monitoring-agent/
sudo chmod +x /opt/monitoring-agent/monitoring-agent
```

#### 2. Spuštění nastavení

```bash
cd /opt/monitoring-agent
sudo ./monitoring-agent register \
  --server-url=https://vas-server.com \
  --email=vas@email.com \
  --password=vaseHeslo \
  --device-name="Můj Server"
```

#### 3. Vytvoření systemd service souboru

Vytvořte soubor `/etc/systemd/system/monitoring-agent.service`:

```ini
[Unit]
Description=Monitoring System Client Agent
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
ExecStart=/opt/monitoring-agent/monitoring-agent
WorkingDirectory=/opt/monitoring-agent
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

#### 4. Aktivace a spuštění služby

```bash
sudo systemctl daemon-reload
sudo systemctl enable monitoring-agent.service
sudo systemctl start monitoring-agent.service
```

#### 5. Kontrola stavu a logů

```bash
# Stav služby
sudo systemctl status monitoring-agent.service

# Živé logy
sudo journalctl -u monitoring-agent.service -f
```

---

## Sestavení ze zdrojových kódů

### Předpoklady

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Sestavení

```bash
dotnet build
```

### Publikace jako samostatný binární soubor

```bash
# Pro x64 Linux
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true

# Pro ARM64 Linux (např. Raspberry Pi)
dotnet publish -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true
```

Výstupní binární soubor se nachází v `bin/Release/net9.0/<runtime>/publish/`.

---

## Struktura projektu

```
Monitoring-system-client-service/
├── Program.cs                  # Vstupní bod — směrování CLI/daemon
├── Worker.cs                   # Služba na pozadí — smyčka sběru metrik
├── CommandHandling/
│   ├── CommandHandler.cs       # Dispatcher CLI příkazů
│   └── CliParser.cs            # Parser argumentů --key=value
├── Configuration/
│   └── ConfigService.cs        # Čtení/zápis TOML konfigurace
├── Models/
│   ├── ConfigModel.cs          # Schéma konfigurace
│   ├── MetricsModel.cs         # DTO sbíraných metrik
│   ├── CreateDeviceModel.cs    # Payload registrace zařízení
│   ├── LoginModel.cs           # Odpověď autentizace
│   └── DeviceWithApiKeyModel.cs
├── Services/
│   ├── ApiClientService.cs     # HTTP klient pro backend API
│   ├── LinuxMetricsService.cs  # Sběr metrik z /proc a /sys
│   ├── SetupService.cs         # Logika nastavení a registrace
│   └── SystemInfoService.cs    # Detekce hardware/sítě
└── agent_config.toml           # Vygenerovaný konfigurační soubor
```

