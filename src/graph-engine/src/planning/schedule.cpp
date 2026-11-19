#include "planning/schedule.h"

#include <algorithm>
#include <functional>
#include <limits>
#include <queue>

namespace coordina::graph
{
namespace
{
constexpr auto no_card = std::numeric_limits<std::size_t>::max();

using CardQueue = std::priority_queue<std::size_t, std::vector<std::size_t>, std::greater<>>;

struct Running
{
  double finish = 0.0;
  std::size_t card = 0U;
};

struct FinishesLater
{
  bool operator()(const Running& left, const Running& right) const noexcept
  {
    if (left.finish > right.finish)
    {
      return true;
    }

    return !(right.finish > left.finish) && left.card > right.card;
  }
};

// One run of the schedule. A card's index is its place in the order people
// pick cards up, so the smallest queued index is the next card to take.
class Simulation
{
public:
  Simulation(
    std::span<const MilestoneCard> cards,
    std::uint32_t person_count,
    std::span<const double> hours)
      : cards_(cards), hours_(hours), remaining_(cards.size()), ready_at_(cards.size(), 0.0),
        ready_cause_(cards.size(), no_card), cause_(cards.size(), no_card),
        started_(cards.size(), 0U), scheduled_(cards.size()), busy_(person_count, 0U),
        last_card_(person_count, no_card), own_queues_(person_count)
  {
    for (std::size_t card = 0U; card < cards.size(); ++card)
    {
      remaining_[card] = cards[card].dependency_count;
    }
  }

  Schedule run()
  {
    for (std::size_t card = 0U; card < cards_.size(); ++card)
    {
      if (remaining_[card] == 0U)
      {
        release(card);
      }
    }

    start_cards(0.0);

    while (!running_.empty())
    {
      const auto now = running_.top().finish;
      finish_cards(now);
      start_cards(now);
    }

    auto chain = driving_chain();
    const auto finish = chain.empty() ? 0.0 : scheduled_[chain.back()].finish_hours;

    return Schedule{
      .cards = std::move(scheduled_),
      .driving_chain = std::move(chain),
      .finish_hours = finish
    };
  }

private:
  void release(std::size_t card)
  {
    if (cards_[card].people.empty())
    {
      shared_queue_.push(card);
      return;
    }

    for (const auto person : cards_[card].people)
    {
      own_queues_[person].push(card);
    }
  }

  void drop_started(CardQueue& queue) const
  {
    while (!queue.empty() && started_[queue.top()] != 0U)
    {
      queue.pop();
    }
  }

  // The first queued card that a free person can take.
  std::size_t next_card()
  {
    auto next = no_card;
    auto anyone_free = false;

    for (std::size_t person = 0U; person < busy_.size(); ++person)
    {
      if (busy_[person] != 0U)
      {
        continue;
      }

      anyone_free = true;
      auto& queue = own_queues_[person];
      drop_started(queue);

      if (!queue.empty())
      {
        next = std::min(next, queue.top());
      }
    }

    if (anyone_free)
    {
      drop_started(shared_queue_);

      if (!shared_queue_.empty())
      {
        next = std::min(next, shared_queue_.top());
      }
    }

    return next;
  }

  // The first free person who can take the card.
  std::uint32_t person_for(std::size_t card) const
  {
    const auto& people = cards_[card].people;

    for (std::uint32_t person = 0U; person < busy_.size(); ++person)
    {
      if (busy_[person] == 0U && (people.empty() || std::ranges::binary_search(people, person)))
      {
        return person;
      }
    }

    return 0U;
  }

  void start_cards(double now)
  {
    for (auto card = next_card(); card != no_card; card = next_card())
    {
      const auto person = person_for(card);
      // A card waits either for its last dependency or, when that was done
      // earlier, for its person to finish what they were doing.
      cause_[card] = ready_at_[card] < now ? last_card_[person] : ready_cause_[card];
      started_[card] = 1U;
      busy_[person] = 1U;
      scheduled_[card] = ScheduledCard{
        .node = cards_[card].node,
        .person = person,
        .start_hours = now,
        .finish_hours = now + hours_[card]
      };
      running_.push(Running{.finish = scheduled_[card].finish_hours, .card = card});
    }
  }

  void finish_cards(double now)
  {
    while (!running_.empty() && !(running_.top().finish > now))
    {
      const auto done = running_.top();
      running_.pop();
      const auto person = scheduled_[done.card].person;
      busy_[person] = 0U;
      last_card_[person] = done.card;

      for (const auto dependent : cards_[done.card].dependents)
      {
        if (!(ready_at_[dependent] > done.finish))
        {
          ready_at_[dependent] = done.finish;
          ready_cause_[dependent] = done.card;
        }

        if (--remaining_[dependent] == 0U)
        {
          release(dependent);
        }
      }
    }
  }

  std::vector<std::size_t> driving_chain() const
  {
    auto last = no_card;

    for (std::size_t card = 0U; card < scheduled_.size(); ++card)
    {
      if (last == no_card || scheduled_[card].finish_hours > scheduled_[last].finish_hours)
      {
        last = card;
      }
    }

    std::vector<std::size_t> chain;

    for (auto card = last; card != no_card; card = cause_[card])
    {
      chain.push_back(card);
    }

    std::ranges::reverse(chain);
    return chain;
  }

  std::span<const MilestoneCard> cards_;
  std::span<const double> hours_;
  std::vector<std::size_t> remaining_;
  std::vector<double> ready_at_;
  std::vector<std::size_t> ready_cause_;
  std::vector<std::size_t> cause_;
  // Flags as bytes: std::vector<bool> costs a bit operation on every access.
  std::vector<std::uint8_t> started_;
  std::vector<ScheduledCard> scheduled_;
  std::vector<std::uint8_t> busy_;
  std::vector<std::size_t> last_card_;
  std::vector<CardQueue> own_queues_;
  CardQueue shared_queue_;
  std::priority_queue<Running, std::vector<Running>, FinishesLater> running_;
};
}

Schedule simulate_schedule(
  std::span<const MilestoneCard> cards,
  std::uint32_t person_count,
  std::span<const double> hours)
{
  return Simulation(cards, person_count, hours).run();
}
}
