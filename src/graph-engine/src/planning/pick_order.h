#ifndef COORDINA_GRAPH_ENGINE_PICK_ORDER_H
#define COORDINA_GRAPH_ENGINE_PICK_ORDER_H

#include "planning/plan_model.h"

#include <cstddef>
#include <vector>

namespace coordina::graph
{
// The order people pick cards up in: cards in progress first, then by the
// latest each card can start without making a milestone late. That date counts
// back from every milestone's target through the chains of cards that wait for
// each other, and through the cards that wait for the same person: a card
// assigned to someone with a lot left to do has to start early even when
// nothing depends on it. Priority only breaks ties, since everything in a
// scope has to ship.
//
// The order depends on the board alone, so it does not change from one
// simulated run to the next. It comes in closed form: on random boards a local
// search over orders finds nothing more than a percent or two better.
//
// Empty when the cards depend on each other in a loop.
std::vector<std::size_t> pick_order(const PlanModel& model);
}

#endif
