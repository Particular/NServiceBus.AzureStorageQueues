namespace NServiceBus.AcceptanceTests.WindowsAzureStorageQueues.Configuration
{
    using System;
    using System.Collections.Generic;
    using global::Newtonsoft.Json.Linq;

    public static class JsonExtensions
    {
        public static List<JProperty> FindProperties(this JToken containerToken, Func<JProperty, bool> predicate)
        {
            var matches = new List<JProperty>();
            FindProperties(containerToken, predicate, matches);
            return matches;
        }

        static void FindProperties(JToken containerToken, Func<JProperty, bool> predicate, List<JProperty> matches)
        {
            if (containerToken.Type == JTokenType.Object)
            {
                foreach (var child in containerToken.Children<JProperty>())
                {
                    if (predicate(child))
                    {
                        matches.Add(child);
                    }
                    FindProperties(child.Value, predicate, matches);
                }
            }
            else if (containerToken.Type == JTokenType.Array)
            {
                foreach (var child in containerToken.Children())
                {
                    FindProperties(child, predicate, matches);
                }
            }
        }
    }
}