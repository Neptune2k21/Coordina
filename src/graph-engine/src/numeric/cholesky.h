#ifndef COORDINA_GRAPH_ENGINE_CHOLESKY_H
#define COORDINA_GRAPH_ENGINE_CHOLESKY_H

#include <cstddef>
#include <span>
#include <vector>

namespace coordina::graph
{
// The lower triangular factor C of a symmetric positive definite matrix
// A = C C^T, stored by rows.
class Cholesky
{
public:
  Cholesky() = default;
  // The matrix is size x size, by rows; only its lower half is read. A pivot
  // that rounding makes too small is raised, so factoring never fails.
  Cholesky(std::vector<double> matrix, std::size_t size);

  std::size_t size() const noexcept;
  // Replaces values with the solution x of A x = values.
  void solve(std::span<double> values) const noexcept;
  // Replaces independent standard normal values with a draw whose covariance
  // is the inverse of A. The last value only moves the draws before it, so it
  // carries its coordinate's whole marginal variance.
  void color(std::span<double> values) const noexcept;

private:
  std::vector<double> factor_;
  std::size_t size_ = 0U;
};
}

#endif
