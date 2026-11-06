#pragma once

// -1 means unmeasured. The previous calibration remains active until all five
// measured values are nonnegative, increasing and within the ESP32 ADC range.
constexpr int WATER_RAW_0 = -1;
constexpr int WATER_RAW_25 = -1;
constexpr int WATER_RAW_50 = -1;
constexpr int WATER_RAW_75 = -1;
constexpr int WATER_RAW_100 = -1;
constexpr int WATER_CALIBRATION_RAW[] = {WATER_RAW_0, WATER_RAW_25, WATER_RAW_50, WATER_RAW_75, WATER_RAW_100};

inline bool validWaterCalibration(const int* points) {
  if (points[0] < 0 || points[4] > 4095) return false;
  for (int i = 1; i < 5; ++i) if (points[i] <= points[i - 1]) return false;
  return true;
}
inline bool waterCalibrationReady() { return validWaterCalibration(WATER_CALIBRATION_RAW); }
inline int interpolateWaterPercent(int raw, const int* points = WATER_CALIBRATION_RAW) {
  if (!validWaterCalibration(points) || raw <= points[0]) return 0;
  if (raw >= points[4]) return 100;
  for (int i = 1; i < 5; ++i) {
    if (raw <= points[i]) return (i - 1) * 25 + (raw - points[i - 1]) * 25 / (points[i] - points[i - 1]);
  }
  return 100;
}
