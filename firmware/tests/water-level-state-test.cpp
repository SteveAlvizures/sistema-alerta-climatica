#include <cassert>
#include "../esp32-vigia-rural/WaterLevelState.h"
int main() {
  assert(waterLevelState(0)==0); assert(waterLevelState(100)==0);
  assert(waterLevelState(101)==1); assert(waterLevelState(1900)==1);
  assert(waterLevelState(1901)==2); assert(waterLevelState(4095)==2);
}
