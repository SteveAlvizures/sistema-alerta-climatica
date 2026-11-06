#pragma once
#include <stdint.h>
#include <stddef.h>

inline int averageWaterRaw(const int* values, size_t count) {
  if (!count) return 0;
  int32_t sum = 0;
  for (size_t i=0;i<count;++i) sum += values[i] < 0 ? 0 : values[i] > 4095 ? 4095 : values[i];
  return static_cast<int>(sum / count);
}
struct LevelCalibrationCapture {
  bool active = false;
  int target = 0, count = 0, minimum = 4095, maximum = 0;
  int32_t sum = 0;
  bool start(int percent) {
    if (percent != 0 && percent != 25 && percent != 50 && percent != 75 && percent != 100) return false;
    target=percent; count=0; minimum=4095; maximum=0; sum=0; active=true; return true;
  }
  bool add(int raw) {
    if (!active) return false;
    if (raw < minimum) minimum=raw;
    if (raw > maximum) maximum=raw;
    sum += raw; ++count;
    if (count == 10) { active=false; return true; }
    return false;
  }
  int average() const { return count ? static_cast<int>(sum/count) : 0; }
};
