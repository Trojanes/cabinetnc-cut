#include "cabinetnc/offset.hpp"

#include <iostream>
#include <string>

int main() {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  std::string input;
  char buf[4096];
  while (std::cin.read(buf, sizeof buf)) {
    input.append(buf, static_cast<size_t>(std::cin.gcount()));
  }
  input.append(buf, static_cast<size_t>(std::cin.gcount()));

  if (input.empty()) {
    std::cout << R"({"ok":false,"error":"empty stdin"})" << '\n';
    return 1;
  }

  const std::string out = cabinetnc::offset_json(input);
  std::cout << out << '\n';
  return out.find("\"ok\":true") != std::string::npos ? 0 : 1;
}
