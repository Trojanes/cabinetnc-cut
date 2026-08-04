#pragma once

#include <string>
#include <utility>
#include <vector>

namespace cabinetnc {

using Point = std::pair<double, double>;
using Ring = std::vector<Point>;

/**
 * General polygon offset via Clipper2 (outward positive / inward negative).
 * Returns zero or more result rings (outer first when Inflate yields several).
 */
std::vector<Ring> offset_polygon(const Ring& points, double delta);

/** Subject minus clips (holes) via Clipper2 Difference. */
std::vector<Ring> boolean_difference(const Ring& subject, const std::vector<Ring>& clips);

/** AABB rect offset — kept for tests / simple callers; uses Clipper under the hood. */
Ring offset_rect(const Ring& points, double delta);

/**
 * JSON dispatcher:
 *  {"op":"offset","delta":n,"polygons":[...]}
 *  {"op":"difference","subject":[[x,y],...],"clips":[[[x,y],...],...]}
 */
std::string offset_json(const std::string& request_json);

}  // namespace cabinetnc
