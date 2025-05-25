using Microsoft.SemanticKernel;
using NodaTime;
using System.ComponentModel;
using System.Globalization;

namespace LocalAIAgent.SemanticKernel.Time
{
    internal class TimeService(IClock clock)
    {
        [KernelFunction, Description("Gets the current time in UTC format.")]
        public string GetCurrentTimeInUtc()
        {
            Console.WriteLine("TimeService: GetCurrentTimeInUtc called");
            return clock.GetCurrentInstant().ToDateTimeUtc().ToString("F", new CultureInfo("en-US"));
        }
    }
}
