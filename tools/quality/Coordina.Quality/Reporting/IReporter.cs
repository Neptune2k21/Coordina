namespace Coordina.Quality.Reporting;

internal interface IReporter
{
  void Report(CheckResult result);
}
