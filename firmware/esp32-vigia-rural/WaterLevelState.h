#pragma once

inline int waterLevelState(int raw) {
  return raw <= 100 ? 0 : raw <= 1900 ? 1 : 2;
}

inline const char* waterLevelLabel(int raw) {
  const char* labels[] = {"Seco", "Nivel bajo", "Nivel alto"};
  return labels[waterLevelState(raw)];
}
