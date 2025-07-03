using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace LocalAIAgent.API.Metrics
{
    public class NewsMetrics : IDisposable
    {
        private readonly Counter<int> _newsRequestsCounter;
        private readonly Histogram<double> _newsRequestDuration;

        private readonly Stopwatch stopwatch = new();

        public NewsMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create("LocalAIAgent.News");
            _newsRequestsCounter = meter.CreateCounter<int>("news.requests.count");
            _newsRequestDuration = meter.CreateHistogram<double>("news.requests.duration");
        }

        public void StartRecordingRequest()
        {
            stopwatch.Start();
            _newsRequestsCounter.Add(1);
        }

        public void StopRecordingRequest()
        {
            stopwatch.Stop();
            _newsRequestDuration.Record(stopwatch.ElapsedMilliseconds);
        }

        public void Dispose()
        {
            stopwatch.Stop();
            GC.SuppressFinalize(this);
        }
    }
}