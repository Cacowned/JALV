using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using JALV.Core.Domain;
using JALV.Core.Exceptions;

namespace JALV.Core.Providers
{
    public class FileEntriesProvider : AbstractEntriesProvider
    {
        private const string Separator = "[---]";
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss,fff";

        public override IEnumerable<LogItem> GetEntries(string dataSource, FilterParams filter)
        {
            if (String.IsNullOrEmpty(dataSource))
                throw new ArgumentNullException("dataSource");
            if (filter == null)
                throw new ArgumentNullException("filter");

            string pattern = filter.Pattern;
            if (String.IsNullOrEmpty(pattern))
                throw new NotValidValueException("filter pattern null");

            FileInfo file = new FileInfo(dataSource);
            if (!file.Exists)
                throw new FileNotFoundException("file not found", dataSource);

            Regex regex = CreateRegex(pattern); // new Regex(@"%\b(date|message|level)\b");
            MatchCollection matches = regex.Matches(pattern);

            LogItem lastEntry = null;

            using (StreamReader reader = file.OpenText())
            {
                string s;
                var entryId = 1;
                while ((s = reader.ReadLine()) != null)
                {
                    string[] items = s.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries);

                    var match = regex.Match(s);

                    if (!match.Success)
                    {
                        lastEntry.Throwable += s;
                        continue;
                    }
                    LogItem entry = CreateEntry(match, entryId++);//items, matches);
                    entry.Logger = filter.Logger;

                    if (lastEntry?.TimeStamp != null)
                        entry.Delta = (entry.TimeStamp - lastEntry.TimeStamp).TotalSeconds;

                    lastEntry = entry;
                    yield return entry;
                }
            }
        }

        private static LogItem CreateEntry(Match match, int entryId)
        {
            LogItem entry = new LogItem()
            {
                Id = entryId
            };

            entry.TimeStamp = DateTime.ParseExact(
                            match.Groups["Date"].Value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

            entry.Message = match.Groups["Message"].Value;
            entry.Level = match.Groups["Level"].Value;
            entry.Thread = match.Groups["Thread"].Value;
            return entry;
        }

        private Regex CreateRegex(string pattern)
        {
            string newPattern = pattern;
            foreach (var kvp in patternRegexMapping)
            {
                newPattern = newPattern.Replace(kvp.Key, kvp.Value);
            }

            return new Regex(newPattern);
        }

        Dictionary<string, string> patternRegexMapping = new Dictionary<string, string>
        {
            { "[", @"\[" },
            { "]", @"\]" },
            { "%date", @"(?<Date>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2},\d{3})" },
            { "%thread", @"(?<Thread>\d+)"},
            { "%-5level", @"(?<Level>[A-Z]{4,5})" },
            { "%logger" , @".*" },
            { "%property{NDC}", @".*" },
            { "%message", @"(?<Message>.*)" },
            { "%newline", "$" },
        };

        private static LogItem CreateEntry(string[] items, MatchCollection matches)
        {
            if (items == null)
                throw new ArgumentNullException("items");
            if (matches == null)
                throw new ArgumentNullException("matches");

            if (items.Length != matches.Count)
                throw new NotValidValueException("different length of items/matches values");

            LogItem entry = new LogItem();
            for (int i = 0; i < matches.Count; i++)
            {
                string value = items[i];
                Match match = matches[i];
                string name = match.Value;
                switch (name)
                {
                    case "%date":
                        entry.TimeStamp = DateTime.ParseExact(
                            value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                        break;

                    case "%message":
                        entry.Message = value;
                        break;

                    case "%level":
                        entry.Level = value;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(name, "unmanaged value");
                }
            }
            return entry;
        }
    }
}