#include <cassert>
#include "../esp32-vigia-rural/WaterCalibration.h"

int main() {
  assert(!waterCalibrationReady()); // Unmeasured firmware retains previous calibration.
  const int fixture[] = {100, 500, 1300, 1900, 2700}; // Synthetic test data only.
  assert(validWaterCalibration(fixture));
  for (int i = 0; i < 5; ++i) assert(interpolateWaterPercent(fixture[i], fixture) == i * 25);
  assert(interpolateWaterPercent(900, fixture) == 37);
  assert(interpolateWaterPercent(2300, fixture) == 87);
  assert(interpolateWaterPercent(0, fixture) == 0);
  assert(interpolateWaterPercent(4095, fixture) == 100);
  const int duplicated[] = {100, 100, 1300, 1900, 2700};
  const int reversed[] = {100, 500, 400, 1900, 2700};
  const int outOfRange[] = {100, 500, 1300, 1900, 5000};
  assert(!validWaterCalibration(duplicated)); assert(!validWaterCalibration(reversed)); assert(!validWaterCalibration(outOfRange));
  for (int raw = 101; raw <= 2700; ++raw) assert(interpolateWaterPercent(raw, fixture) >= interpolateWaterPercent(raw - 1, fixture));
}
