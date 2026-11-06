#include <cassert>
#include "../esp32-vigia-rural/LevelSampling.h"
#include "../esp32-vigia-rural/WaterCalibration.h"

int main() {
  const int noisy[] = {0,20,40,30,10}; assert(averageWaterRaw(noisy,5)==20);
  const int zero[] = {0,0,0}; assert(averageWaterRaw(zero,3)==0);
  LevelCalibrationCapture capture;
  assert(!capture.start(33)); assert(capture.start(50));
  for(int i=0;i<9;++i) assert(!capture.add(i*10));
  assert(capture.add(90));assert(capture.average()==45);assert(capture.minimum==0);assert(capture.maximum==90);assert(!capture.active);
  assert(!capture.add(200));assert(capture.start(0));assert(capture.count==0);

  assert(waterCalibrationReady());
  const int demo[] = {0, 364, 728, 1091, 1455};
  for (int i = 0; i < 5; ++i) {
    assert(WATER_CALIBRATION_RAW[i] == demo[i]);
    assert(interpolateWaterPercent(demo[i]) == i * 25);
  }
  assert(interpolateWaterPercent(-1) == 0);
  assert(interpolateWaterPercent(4095) == 100);
  for (int raw = 1; raw <= 4095; ++raw)
    assert(interpolateWaterPercent(raw) >= interpolateWaterPercent(raw - 1));
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
