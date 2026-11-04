#pragma once

// Copiar a secrets.h (ignorado por Git) y completar localmente.
// WiFi de 2.4 GHz. No compartir secrets.h ni capturas con sus valores.
constexpr char WIFI_SSID[] = "";
constexpr char WIFI_PASSWORD[] = "";
constexpr char DEVICE_KEY_TEMPERATURE[] = "";
// HTTPS, IP LAN o nombre incluido en el certificado; sin barra final ni /api.
constexpr char API_BASE_URL[] = "https://172.20.10.4:18444";
constexpr char TEMPERATURE_SENSOR_ID[] = "";
constexpr char HUMIDITY_SENSOR_ID[] = "";
constexpr char RAIN_SENSOR_ID[] = "";
constexpr char WATER_LEVEL_SENSOR_ID[] = "";
constexpr char DEVICE_KEY_HUMIDITY[] = "";
constexpr char DEVICE_KEY_RAIN[] = "";
constexpr char DEVICE_KEY_WATER_LEVEL[] = "";
// Certificado público de la CA emisora, en formato PEM. Nunca su clave privada.
constexpr char API_ROOT_CA[] = "";
