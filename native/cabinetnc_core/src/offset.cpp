#include "cabinetnc/offset.hpp"

#include "clipper2/clipper.h"

#include <algorithm>
#include <cctype>
#include <cmath>
#include <sstream>
#include <stdexcept>

namespace cabinetnc {
namespace {

using Clipper2Lib::Difference;
using Clipper2Lib::EndType;
using Clipper2Lib::FillRule;
using Clipper2Lib::InflatePaths;
using Clipper2Lib::JoinType;
using Clipper2Lib::PathD;
using Clipper2Lib::PathsD;
using Clipper2Lib::PointD;

struct BBox {
  double min_x = 0;
  double min_y = 0;
  double max_x = 0;
  double max_y = 0;
  double width() const { return max_x - min_x; }
  double height() const { return max_y - min_y; }
};

BBox bbox(const Ring& pts) {
  BBox b;
  if (pts.empty()) return b;
  b.min_x = b.max_x = pts[0].first;
  b.min_y = b.max_y = pts[0].second;
  for (const auto& p : pts) {
    if (p.first < b.min_x) b.min_x = p.first;
    if (p.second < b.min_y) b.min_y = p.second;
    if (p.first > b.max_x) b.max_x = p.first;
    if (p.second > b.max_y) b.max_y = p.second;
  }
  return b;
}

double ring_area_abs(const Ring& pts) {
  if (pts.size() < 3) return 0;
  double a = 0;
  for (size_t i = 0, n = pts.size(); i < n; ++i) {
    const auto& p0 = pts[i];
    const auto& p1 = pts[(i + 1) % n];
    a += p0.first * p1.second - p1.first * p0.second;
  }
  return std::abs(a) * 0.5;
}

PathD to_path(const Ring& ring) {
  PathD path;
  path.reserve(ring.size());
  for (const auto& p : ring) {
    // skip duplicate closing vertex Clipper doesn't need
    if (!path.empty() && path.front().x == p.first && path.front().y == p.second) continue;
    path.push_back(PointD(p.first, p.second));
  }
  return path;
}

Ring from_path(const PathD& path) {
  Ring ring;
  ring.reserve(path.size());
  for (const auto& p : path) ring.push_back({p.x, p.y});
  return ring;
}

void skip_ws(const std::string& s, size_t& i) {
  while (i < s.size() && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t')) ++i;
}

bool expect(const std::string& s, size_t& i, char c) {
  skip_ws(s, i);
  if (i >= s.size() || s[i] != c) return false;
  ++i;
  return true;
}

bool parse_number(const std::string& s, size_t& i, double& out) {
  skip_ws(s, i);
  size_t start = i;
  if (i < s.size() && (s[i] == '-' || s[i] == '+')) ++i;
  bool any = false;
  while (i < s.size() && std::isdigit(static_cast<unsigned char>(s[i]))) {
    any = true;
    ++i;
  }
  if (i < s.size() && s[i] == '.') {
    ++i;
    while (i < s.size() && std::isdigit(static_cast<unsigned char>(s[i]))) {
      any = true;
      ++i;
    }
  }
  if (!any) return false;
  if (i < s.size() && (s[i] == 'e' || s[i] == 'E')) {
    size_t e = i + 1;
    if (e < s.size() && (s[e] == '+' || s[e] == '-')) ++e;
    size_t exp_digits = e;
    while (e < s.size() && std::isdigit(static_cast<unsigned char>(s[e]))) ++e;
    if (e > exp_digits) i = e;
  }
  try {
    out = std::stod(s.substr(start, i - start));
  } catch (...) {
    return false;
  }
  return true;
}

bool parse_point(const std::string& s, size_t& i, Point& p) {
  if (!expect(s, i, '[')) return false;
  if (!parse_number(s, i, p.first)) return false;
  if (!expect(s, i, ',')) return false;
  if (!parse_number(s, i, p.second)) return false;
  return expect(s, i, ']');
}

bool parse_ring(const std::string& s, size_t& i, Ring& ring) {
  if (!expect(s, i, '[')) return false;
  skip_ws(s, i);
  if (i < s.size() && s[i] == ']') {
    ++i;
    return true;
  }
  for (;;) {
    Point p;
    if (!parse_point(s, i, p)) return false;
    ring.push_back(p);
    skip_ws(s, i);
    if (i < s.size() && s[i] == ',') {
      ++i;
      continue;
    }
    return expect(s, i, ']');
  }
}

bool find_key(const std::string& s, const char* key, size_t& after_colon) {
  const std::string needle = std::string("\"") + key + "\"";
  size_t pos = s.find(needle);
  if (pos == std::string::npos) return false;
  pos += needle.size();
  skip_ws(s, pos);
  if (pos >= s.size() || s[pos] != ':') return false;
  after_colon = pos + 1;
  return true;
}

std::string escape_err(const std::string& msg) {
  std::string o;
  for (char c : msg) {
    if (c == '"' || c == '\\') o.push_back('\\');
    o.push_back(c);
  }
  return o;
}

std::string ring_to_json(const Ring& r) {
  std::ostringstream os;
  os.setf(std::ios::fixed);
  os.precision(6);
  os << '[';
  for (size_t i = 0; i < r.size(); ++i) {
    if (i) os << ',';
    os << '[' << r[i].first << ',' << r[i].second << ']';
  }
  os << ']';
  return os.str();
}

}  // namespace

std::vector<Ring> offset_polygon(const Ring& points, double delta) {
  if (points.size() < 3) return {};
  PathsD subject;
  subject.push_back(to_path(points));
  if (subject[0].size() < 3) return {};

  // precision=2 → 0.01 mm; enough for cabinet nesting, cheap.
  PathsD solution = InflatePaths(subject, delta, JoinType::Miter, EndType::Polygon, 2.0, 2);

  std::vector<Ring> out;
  out.reserve(solution.size());
  for (const auto& path : solution) {
    if (path.size() < 3) continue;
    out.push_back(from_path(path));
  }
  // Largest area first (outer preference for single-contour callers).
  std::sort(out.begin(), out.end(), [](const Ring& a, const Ring& b) {
    return ring_area_abs(a) > ring_area_abs(b);
  });
  return out;
}

Ring offset_rect(const Ring& points, double delta) {
  auto rings = offset_polygon(points, delta);
  if (!rings.empty()) return rings[0];
  // Fallback AABB if Clipper yields nothing (e.g. collapsed inward).
  const BBox box = bbox(points);
  const double w = box.width() + 2.0 * delta;
  const double h = box.height() + 2.0 * delta;
  if (w <= 0.0 || h <= 0.0) return {};
  return {
      {box.min_x - delta, box.min_y - delta},
      {box.min_x - delta + w, box.min_y - delta},
      {box.min_x - delta + w, box.min_y - delta + h},
      {box.min_x - delta, box.min_y - delta + h},
  };
}

std::vector<Ring> boolean_difference(const Ring& subject, const std::vector<Ring>& clips) {
  if (subject.size() < 3) return {};
  PathsD subj;
  subj.push_back(to_path(subject));
  PathsD clip;
  for (const auto& c : clips) {
    if (c.size() < 3) continue;
    clip.push_back(to_path(c));
  }
  PathsD solution = Difference(subj, clip, FillRule::NonZero, 2);
  std::vector<Ring> out;
  out.reserve(solution.size());
  for (const auto& path : solution) {
    if (path.size() < 3) continue;
    out.push_back(from_path(path));
  }
  std::sort(out.begin(), out.end(), [](const Ring& a, const Ring& b) {
    return ring_area_abs(a) > ring_area_abs(b);
  });
  return out;
}

bool parse_rings_array(const std::string& s, size_t& i, std::vector<Ring>& out) {
  if (!expect(s, i, '[')) return false;
  skip_ws(s, i);
  if (i < s.size() && s[i] == ']') {
    ++i;
    return true;
  }
  for (;;) {
    Ring ring;
    if (!parse_ring(s, i, ring)) return false;
    out.push_back(std::move(ring));
    skip_ws(s, i);
    if (i < s.size() && s[i] == ',') {
      ++i;
      continue;
    }
    return expect(s, i, ']');
  }
}

std::string offset_json(const std::string& request_json) {
  try {
    size_t op_i = 0;
    std::string op = "offset";
    if (find_key(request_json, "op", op_i)) {
      // crude string parse: "difference" or "offset"
      skip_ws(request_json, op_i);
      if (op_i < request_json.size() && request_json[op_i] == '"') {
        ++op_i;
        size_t end = request_json.find('"', op_i);
        if (end != std::string::npos) op = request_json.substr(op_i, end - op_i);
      }
    }

    if (op == "difference") {
      size_t si = 0;
      Ring subject;
      if (!find_key(request_json, "subject", si) || !parse_ring(request_json, si, subject)) {
        return R"({"ok":false,"error":"missing subject"})";
      }
      size_t ci = 0;
      std::vector<Ring> clips;
      if (!find_key(request_json, "clips", ci) || !parse_rings_array(request_json, ci, clips)) {
        return R"({"ok":false,"error":"missing clips"})";
      }
      auto out = boolean_difference(subject, clips);
      std::ostringstream os;
      os << R"({"ok":true,"engine":"cabinetnc_core","mode":"clipper_difference","polygons":[)";
      for (size_t k = 0; k < out.size(); ++k) {
        if (k) os << ',';
        os << ring_to_json(out[k]);
      }
      os << "]}";
      return os.str();
    }

    size_t i = 0;
    double delta = 0;
    if (!find_key(request_json, "delta", i) || !parse_number(request_json, i, delta)) {
      return R"({"ok":false,"error":"missing delta"})";
    }

    size_t poly_i = 0;
    if (!find_key(request_json, "polygons", poly_i)) {
      return R"({"ok":false,"error":"missing polygons"})";
    }
    std::vector<Ring> polys;
    if (!parse_rings_array(request_json, poly_i, polys)) {
      return R"({"ok":false,"error":"bad polygons"})";
    }

    std::vector<Ring> out;
    for (const auto& ring : polys) {
      auto inflated = offset_polygon(ring, delta);
      for (auto& r : inflated) out.push_back(std::move(r));
    }

    std::ostringstream os;
    os << R"({"ok":true,"engine":"cabinetnc_core","mode":"clipper_offset","polygons":[)";
    for (size_t k = 0; k < out.size(); ++k) {
      if (k) os << ',';
      os << ring_to_json(out[k]);
    }
    os << "]}";
    return os.str();
  } catch (const std::exception& e) {
    return std::string(R"({"ok":false,"error":")") + escape_err(e.what()) + "\"}";
  }
}

}  // namespace cabinetnc
