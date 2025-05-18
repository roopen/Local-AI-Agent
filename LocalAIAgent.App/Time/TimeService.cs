using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LocalAIAgent.App.Time
{
    internal class TimeService
    {
        [KernelFunction, Description("Gets the current time in UTC format.")]
        public string GetCurrentTimeInUtc()
        {
            Console.WriteLine("TimeService: GetCurrentTimeInUtc called");
            return DateTime.UtcNow.ToString();
        }
    }
}
