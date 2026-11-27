#include "planning/planning_board.h"

namespace coordina::graph
{
const char* describe(PlanError error) noexcept
{
  switch (error)
  {
    case PlanError::unknown_milestone:
      return "A milestone is not a node of the board.";
    case PlanError::unknown_person:
      return "A person is outside person_count.";
    case PlanError::bad_number:
      return "A number is not finite, or is outside its range.";
    case PlanError::cycle:
      return "Open cards depend on each other in a loop.";
    case PlanError::none:
      break;
  }

  return "";
}
}
