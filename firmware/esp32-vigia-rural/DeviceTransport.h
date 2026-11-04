#pragma once
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <HTTPClient.h>
#include <Preferences.h>
#include <esp_random.h>
#include <freertos/FreeRTOS.h>
#include <freertos/queue.h>
#include <freertos/task.h>
#include <time.h>

#if __has_include("secrets.h")
#include "secrets.h"
#else
#include "secrets.example.h"
#endif

constexpr unsigned long API_SEND_INTERVAL_MS = 2000;
constexpr unsigned long WIFI_CONNECT_TIMEOUT_MS = 15000;
constexpr unsigned long WIFI_RETRY_INTERVAL_MS = 30000;
constexpr unsigned long API_CYCLE_BUDGET_MS = 1600;
constexpr size_t CHANNEL_COUNT = 4;
struct ChannelConfig { const char* label; const char* sensorId; const char* key; const char* nvs; };
const ChannelConfig channels[CHANNEL_COUNT] = {
  {"Temperatura", TEMPERATURE_SENSOR_ID, DEVICE_KEY_TEMPERATURE, "vigia-temp"},
  {"Humedad", HUMIDITY_SENSOR_ID, DEVICE_KEY_HUMIDITY, "vigia-hum"},
  {"Lluvia", RAIN_SENSOR_ID, DEVICE_KEY_RAIN, "vigia-rain"},
  {"Nivel", WATER_LEVEL_SENSOR_ID, DEVICE_KEY_WATER_LEVEL, "vigia-level"},
};
struct SensorSample { float values[CHANNEL_COUNT]; bool valid[CHANNEL_COUNT]; unsigned long sampledAt; };
// Binary layout retained from Block 5: existing temperature pending survives this update.
struct PendingReading { uint32_t version; int32_t tenthsC; char sensorId[37]; char messageId[37]; };
struct ChannelState {
  Preferences storage;
  PendingReading pending{};
  bool present = false, paused = false, attempted = false, sampled = false;
  unsigned long lastAttempt = 0, lastSampled = 0;
};
QueueHandle_t sensorQueue = nullptr;
WiFiClientSecure apiTls;
HTTPClient apiHttp;

bool isUuid(const char* value) {
  if (strlen(value) != 36) return false;
  bool nonzero = false;
  for (int i = 0; i < 36; ++i) {
    if (i == 8 || i == 13 || i == 18 || i == 23) { if (value[i] != '-') return false; }
    else { if (!isxdigit(static_cast<unsigned char>(value[i]))) return false; nonzero |= value[i] != '0'; }
  }
  return nonzero;
}
bool channelConfigured(size_t i) {
  if (!isUuid(channels[i].sensorId) || strlen(channels[i].key) != 64) return false;
  for (const char* p = channels[i].key; *p; ++p) if (!isxdigit(static_cast<unsigned char>(*p))) return false;
  return true;
}
bool validReading(size_t i, float value) {
  if (!isfinite(value)) return false;
  if (i == 0) return value >= -100 && value <= 100;
  if (i == 2) return value == 0 || value == 1 || value == 2;
  return value >= 0 && value <= 100;
}
void makeMessageId(char (&id)[37]) {
  uint8_t b[16]; esp_fill_random(b, sizeof(b));  // WiFi/RF active.
  b[6] = (b[6] & 0x0f) | 0x40; b[8] = (b[8] & 0x3f) | 0x80;
  snprintf(id, sizeof(id), "%02x%02x%02x%02x-%02x%02x-%02x%02x-%02x%02x-%02x%02x%02x%02x%02x%02x",
           b[0],b[1],b[2],b[3],b[4],b[5],b[6],b[7],b[8],b[9],b[10],b[11],b[12],b[13],b[14],b[15]);
}
int sendReading(const char* sensorId, float value, const char* deviceKey, const char* messageId) {
  char body[180];
  snprintf(body, sizeof(body), "{\"sensorId\":\"%s\",\"value\":%.1f,\"messageId\":\"%s\"}",
           sensorId, static_cast<double>(value), messageId);
  apiHttp.addHeader("Content-Type", "application/json");
  apiHttp.addHeader("X-Device-Key", deviceKey);  // Replaces previous sensor's header.
  const int status = apiHttp.POST(reinterpret_cast<uint8_t*>(body), strlen(body));
  // Consume the small response so a validated TLS connection can be reused.
  // Never log bodies, keys, certificates or passwords.
  if (status > 0) apiHttp.getString();
  return status;
}
void sensorTransportTask(void*) {
  ChannelState state[CHANNEL_COUNT];
  for (size_t i = 0; i < CHANNEL_COUNT; ++i) {
    auto& s = state[i];
    if (!channelConfigured(i) || !s.storage.begin(channels[i].nvs, false)) { s.paused = true; continue; }
    const size_t length = s.storage.getBytesLength("pending");
    if (length) {
      if (length != sizeof(s.pending) || s.storage.getBytes("pending", &s.pending, sizeof(s.pending)) != sizeof(s.pending)
          || s.pending.version != 1 || s.pending.sensorId[36] != '\0' || s.pending.messageId[36] != '\0'
          || !isUuid(s.pending.messageId) || strcmp(s.pending.sensorId, channels[i].sensorId) != 0
          || !validReading(i, s.pending.tenthsC / 10.0f)) {
        s.paused = true; Serial.printf("%s: pendiente NVS incompatible; revisar configuracion\n", channels[i].label);
      } else { s.present = true; Serial.printf("%s: pendiente recuperado con el mismo UUID\n", channels[i].label); }
    }
  }
  apiTls.setCACert(API_ROOT_CA); apiTls.setHandshakeTimeout(1);
  apiHttp.setConnectTimeout(800); apiHttp.setTimeout(800); apiHttp.setReuse(true);
  apiHttp.setFollowRedirects(HTTPC_DISABLE_FOLLOW_REDIRECTS);
  String base(API_BASE_URL); while (base.endsWith("/")) base.remove(base.length()-1);
  if (!apiHttp.begin(apiTls, base + "/api/device-readings")) { vTaskDelete(nullptr); return; }
  WiFi.mode(WIFI_STA); WiFi.setAutoReconnect(false);
  unsigned long lastWifiAttempt = 0, lastCycle = millis();
  bool attemptedWifi = false, connecting = false, wasConnected = false, clockStarted = false, clockNotice = false;
  size_t firstChannel = 0;
  for (;;) {
    const unsigned long now = millis();
    const bool connected = WiFi.status() == WL_CONNECTED;
    if (!connected) {
      if (wasConnected) { Serial.println("WiFi desconectado; reconexion controlada"); wasConnected = false; }
      if (connecting && now-lastWifiAttempt >= WIFI_CONNECT_TIMEOUT_MS) {
        WiFi.disconnect(); connecting = false; Serial.println("Timeout WiFi; diagnostico sigue activo");
      }
      if (!connecting && (!attemptedWifi || now-lastWifiAttempt >= WIFI_RETRY_INTERVAL_MS)) {
        Serial.println("Conectando a WiFi..."); WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
        lastWifiAttempt = now; attemptedWifi = true; connecting = true;
      }
    } else {
      connecting = false;
      if (!wasConnected) { Serial.print("WiFi conectado: "); Serial.println(WiFi.localIP()); wasConnected = true; }
      if (!clockStarted) { configTime(0,0,"pool.ntp.org","time.cloudflare.com"); clockStarted = true; }
      if (time(nullptr) < 1793491200) {
        if (!clockNotice) Serial.println("Esperando hora NTP para validar TLS"); clockNotice = true;
      } else if (now-lastCycle >= API_SEND_INTERVAL_MS) {
        lastCycle = now;
        SensorSample sample{};
        const bool recent = xQueuePeek(sensorQueue, &sample, 0) == pdTRUE && now-sample.sampledAt <= 5000;
        const unsigned long cycleStart = millis();
        const size_t start = firstChannel;
        Serial.println("--- Enviando lecturas ---");
        for (size_t offset = 0; offset < CHANNEL_COUNT; ++offset) {
          if (millis()-cycleStart >= API_CYCLE_BUDGET_MS) { Serial.println("Ciclo parcial; se continuara sin acumular muestras"); break; }
          const size_t i = (start+offset) % CHANNEL_COUNT; auto& s = state[i];
          firstChannel = (i+1) % CHANNEL_COUNT;  // Fair progress even when a channel times out.
          const unsigned long attemptAt = millis();
          if (s.paused || (s.attempted && attemptAt-s.lastAttempt < API_SEND_INTERVAL_MS)) continue;
          if (!s.present) {
            if (!recent || !sample.valid[i] || (s.sampled && sample.sampledAt == s.lastSampled)) continue;
            s.pending = {}; s.pending.version = 1; s.pending.tenthsC = lroundf(sample.values[i]*10.0f);
            strlcpy(s.pending.sensorId, channels[i].sensorId, sizeof(s.pending.sensorId)); makeMessageId(s.pending.messageId);
            if (s.storage.putBytes("pending", &s.pending, sizeof(s.pending)) != sizeof(s.pending)) {
              s.paused = true; Serial.printf("%s: error NVS; otros sensores continuan\n", channels[i].label); continue;
            }
            s.present = true; s.lastSampled = sample.sampledAt; s.sampled = true;
          }
          s.lastAttempt = attemptAt; s.attempted = true;
          const int status = sendReading(s.pending.sensorId, s.pending.tenthsC/10.0f, channels[i].key, s.pending.messageId);
          Serial.printf("%s -> HTTP %d\n", channels[i].label, status);
          if (status == 200 || status == 201) {
            Serial.println(status == 201 ? "Lectura creada" : "Reintento idempotente aceptado");
            if (s.storage.remove("pending")) s.present = false; else s.paused = true;
          } else {
            Serial.printf("%s: valor/UUID conservados\n", channels[i].label);
            if (status >= 300 && status < 500 && status != 408 && status != 429) s.paused = true;
          }
        }
        Serial.println("Ciclo completado");
      }
    }
    vTaskDelay(pdMS_TO_TICKS(20));
  }
}
void beginSensorTransport() {
  bool any = false; for (size_t i=0; i<CHANNEL_COUNT; ++i) any |= channelConfigured(i);
  if (!any || !strlen(WIFI_SSID) || !strlen(API_ROOT_CA) || !String(API_BASE_URL).startsWith("https://")) {
    Serial.println("Configurar secrets.h local; solo diagnostico Serial activo"); return;
  }
  sensorQueue = xQueueCreate(1, sizeof(SensorSample));
  if (!sensorQueue) return;
  // Network/TLS never block the sensor loop on core 1.
  if (xTaskCreatePinnedToCore(sensorTransportTask,"sensor-api",12288,nullptr,1,nullptr,0) != pdPASS) {
    vQueueDelete(sensorQueue); sensorQueue = nullptr;
  }
}
void publishSensorSample(float temperature, float humidity, int rain, int level, unsigned long sampledAt) {
  if (!sensorQueue) return;
  SensorSample sample{{temperature,humidity,static_cast<float>(rain),static_cast<float>(level)}, {}, sampledAt};
  for (size_t i=0; i<CHANNEL_COUNT; ++i) sample.valid[i] = validReading(i,sample.values[i]);
  xQueueOverwrite(sensorQueue,&sample);
}
