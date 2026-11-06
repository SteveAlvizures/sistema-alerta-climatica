#include <Arduino.h>
#include <DHT.h>
#include <WiFi.h>
#include "DeviceTransport.h"
#include "WaterCalibration.h"
#include "LevelSampling.h"
#include "WaterLevelState.h"

constexpr uint8_t DHT_PIN = 4;
constexpr uint8_t RAIN_PIN = 34;
constexpr uint8_t WATER_LEVEL_PIN = 35;
constexpr unsigned long SENSOR_SAMPLE_INTERVAL_MS = 2000;
constexpr int RAIN_DRY_THRESHOLD = 3600;
constexpr int RAIN_LIGHT_THRESHOLD = 1800;
constexpr uint8_t RAIN_SAMPLE_COUNT = 5;
constexpr int WATER_EMPTY = 0;
constexpr int WATER_FULL = 1630;

const char* rainState(int raw) {
  if (raw >= RAIN_DRY_THRESHOLD) return "Seco";
  if (raw >= RAIN_LIGHT_THRESHOLD) return "Mojado leve";
  return "Muy mojado";
}

int readRainRaw() {
  long total = 0;
  for (uint8_t i = 0; i < RAIN_SAMPLE_COUNT; ++i) {
    total += analogRead(RAIN_PIN);
  }
  return static_cast<int>(total / RAIN_SAMPLE_COUNT);
}

// Serial calibration is nonblocking. Enter 0, 25, 50, 75 or 100 followed by newline.
constexpr bool WATER_CALIBRATION_MODE = false; // Categorical level has no percentage calibration.
LevelCalibrationCapture levelCapture;
void readCalibrationCommand() {
  if (!WATER_CALIBRATION_MODE) return;
  static char command[8] = {}; static size_t length = 0;
  while (Serial.available()) {
    const char c = Serial.read();
    if (c == '\r') continue;
    if (c == '\n') {
      command[length] = 0;
      bool valid = length > 0;
      for (size_t i=0;i<length;++i) valid = valid && command[i]>='0' && command[i]<='9';
      if (valid && levelCapture.start(atoi(command))) Serial.printf("CALIBRACION NIVEL %d %%: tomando 10 muestras RAW filtradas\n",levelCapture.target);
      else Serial.println("Posiciones validas: 0, 25, 50, 75, 100; termine con nueva linea");
      length=0;
    } else if (length < sizeof(command)-1) command[length++]=c;
    else { length=0; Serial.println("Comando demasiado largo; reintente"); }
  }
}
int readWaterRaw() {
  int samples[8];
  for (size_t i=0;i<8;++i) samples[i]=analogRead(WATER_LEVEL_PIN);
  return averageWaterRaw(samples,8);
}

int waterPercent(int raw) {
  if (waterCalibrationReady()) return interpolateWaterPercent(raw);
  if (raw <= 100) return 0;
  const long percent = (static_cast<long>(raw) - WATER_EMPTY) * 100L /
                       (WATER_FULL - WATER_EMPTY);
  return static_cast<int>(constrain(percent, 0L, 100L));
}

DHT dht(DHT_PIN, DHT11);
unsigned long lastSampleAt = 0;

void setup() {
  Serial.begin(115200);
  dht.begin();

  pinMode(RAIN_PIN, INPUT);
  pinMode(WATER_LEVEL_PIN, INPUT);
  analogReadResolution(12);  // RAW: 0..4095; no conversion to physical units.
  analogSetPinAttenuation(RAIN_PIN, ADC_11db);
  analogSetPinAttenuation(WATER_LEVEL_PIN, ADC_11db);

  // Allow the DHT11 to settle before the first measurement.
  lastSampleAt = millis();
  beginSensorTransport();
}

void loop() {
  readCalibrationCommand();
  const unsigned long now = millis();
  // Unsigned subtraction also works when millis() wraps around.
  if (now - lastSampleAt < SENSOR_SAMPLE_INTERVAL_MS) {
    return;
  }
  lastSampleAt = now;

  const float temperature = dht.readTemperature();
  const float humidity = dht.readHumidity();
  const int rainRaw = readRainRaw();
  const int waterLevelRaw = readWaterRaw();
  if (WATER_CALIBRATION_MODE && levelCapture.add(waterLevelRaw)) {
    Serial.printf("CALIBRACION NIVEL %d %%\nMuestras: %d\nRAW promedio: %d\nRAW min: %d\nRAW max: %d\n", levelCapture.target,levelCapture.count,levelCapture.average(),levelCapture.minimum,levelCapture.maximum);
  }
  const int rainValue = rainRaw >= RAIN_DRY_THRESHOLD ? 0 : (rainRaw >= RAIN_LIGHT_THRESHOLD ? 1 : 2);
  publishSensorSample(temperature, humidity, rainValue, waterLevelState(waterLevelRaw), now);

  if (isnan(temperature)) {
    Serial.println("Temperatura: ERROR DHT11");
  } else {
    Serial.print("Temperatura: ");
    Serial.print(temperature, 1);
    Serial.println(" C");
  }

  if (isnan(humidity)) {
    Serial.println("Humedad: ERROR DHT11");
  } else {
    Serial.print("Humedad: ");
    Serial.print(humidity, 1);
    Serial.println(" %");
  }

  // Always report analog channels, even if the DHT11 fails.
  Serial.print("Lluvia: ");
  Serial.print(rainState(rainRaw));
  Serial.print(" (RAW: ");
  Serial.print(rainRaw);
  Serial.println(")");
  Serial.print("Nivel: ");
  Serial.print(waterLevelLabel(waterLevelRaw));
  Serial.print(" (RAW: ");
  Serial.print(waterLevelRaw);
  Serial.println(")");
  Serial.println();
}
