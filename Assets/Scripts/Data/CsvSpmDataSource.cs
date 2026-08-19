using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ShenhuSpm
{
    public sealed class CsvSpmDataSource : ISpmDataSource
    {
        private readonly string filePath;
        private readonly SpmStation defaultStation;

        public CsvSpmDataSource(string filePath, SpmStation defaultStation)
        {
            this.filePath = filePath;
            this.defaultStation = defaultStation;
        }

        public List<SpmRecord> Load()
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A CSV file path is required.", "filePath");
            }

            return Parse(File.ReadAllText(filePath, Encoding.UTF8), defaultStation);
        }

        public static List<SpmRecord> Parse(string csvText, SpmStation defaultStation)
        {
            if (string.IsNullOrWhiteSpace(csvText))
            {
                throw new ArgumentException("CSV content is empty.", "csvText");
            }

            string[] lines = csvText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length < 2)
            {
                throw new FormatException("CSV content must contain a header and at least one data row.");
            }

            List<string> headers = ParseCsvLine(lines[0]);
            Dictionary<string, int> index = BuildHeaderIndex(headers);
            int timestampIndex = FindColumn(index, "timestamp", "time", "date_time", "时间");
            int elapsedIndex = FindColumn(index, "elapsed_hours", "hour", "hours", "小时");
            int turbidityIndex = FindColumn(index, "turbidity_ntu", "turbidity", "ntu", "浊度");
            int spmIndex = FindColumn(index, "spm_mg_l", "spm", "concentration", "suspended_matter_concentration", "悬浮物浓度");
            int stationIndex = FindColumn(index, "station", "site", "站点");

            if (elapsedIndex < 0 && timestampIndex < 0)
            {
                throw new FormatException("CSV requires either elapsed_hours or timestamp.");
            }

            if (turbidityIndex < 0 && spmIndex < 0)
            {
                throw new FormatException("CSV requires either turbidity_ntu or spm_mg_l.");
            }

            List<SpmRecord> records = new List<SpmRecord>();
            DateTime? firstTimestamp = null;

            for (int lineNumber = 2; lineNumber <= lines.Length; lineNumber++)
            {
                string raw = lines[lineNumber - 1];
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                List<string> cells = ParseCsvLine(raw);
                SpmRecord record = new SpmRecord();
                record.Station = ParseStation(GetCell(cells, stationIndex), defaultStation);

                string timestampText = GetCell(cells, timestampIndex);
                DateTime timestamp;
                if (TryParseTimestamp(timestampText, out timestamp))
                {
                    record.Timestamp = timestamp;
                    if (!firstTimestamp.HasValue)
                    {
                        firstTimestamp = timestamp;
                    }
                }

                double elapsed;
                if (TryParseDouble(GetCell(cells, elapsedIndex), out elapsed))
                {
                    record.ElapsedHours = elapsed;
                }
                else if (firstTimestamp.HasValue && record.Timestamp != DateTime.MinValue)
                {
                    record.ElapsedHours = (record.Timestamp - firstTimestamp.Value).TotalHours;
                }
                else
                {
                    throw new FormatException("Cannot determine elapsed time at CSV line " + lineNumber + ".");
                }

                double value;
                if (TryParseDouble(GetCell(cells, turbidityIndex), out value))
                {
                    record.TurbidityNtu = value;
                }

                if (TryParseDouble(GetCell(cells, spmIndex), out value))
                {
                    record.SpmMgPerL = value;
                }

                records.Add(record);
            }

            records.Sort(delegate(SpmRecord left, SpmRecord right)
            {
                return left.ElapsedHours.CompareTo(right.ElapsedHours);
            });

            if (records.Count == 0)
            {
                throw new FormatException("CSV content contains no usable data rows.");
            }

            return records;
        }

        private static Dictionary<string, int> BuildHeaderIndex(List<string> headers)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
            {
                string normalized = NormalizeHeader(headers[i]);
                if (!result.ContainsKey(normalized))
                {
                    result.Add(normalized, i);
                }
            }

            return result;
        }

        private static int FindColumn(Dictionary<string, int> index, params string[] aliases)
        {
            foreach (string alias in aliases)
            {
                int value;
                if (index.TryGetValue(NormalizeHeader(alias), out value))
                {
                    return value;
                }
            }

            return -1;
        }

        private static string NormalizeHeader(string header)
        {
            if (header == null)
            {
                return string.Empty;
            }

            string normalized = header.Trim().TrimStart('\uFEFF').ToLowerInvariant();
            normalized = normalized.Replace(" ", "_").Replace("-", "_");
            int openingParenthesis = normalized.IndexOf('(');
            int fullWidthOpeningParenthesis = normalized.IndexOf('（');
            if (openingParenthesis < 0 ||
                (fullWidthOpeningParenthesis >= 0 && fullWidthOpeningParenthesis < openingParenthesis))
            {
                openingParenthesis = fullWidthOpeningParenthesis;
            }

            if (openingParenthesis > 0)
            {
                normalized = normalized.Substring(0, openingParenthesis).TrimEnd('_');
            }

            return normalized;
        }

        private static string GetCell(List<string> cells, int index)
        {
            return index >= 0 && index < cells.Count ? cells[index].Trim() : string.Empty;
        }

        private static SpmStation ParseStation(string value, SpmStation fallback)
        {
            SpmStation parsed;
            return Enum.TryParse(value, true, out parsed) ? parsed : fallback;
        }

        private static bool TryParseDouble(string value, out double parsed)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Equals("NA", StringComparison.OrdinalIgnoreCase))
            {
                parsed = double.NaN;
                return false;
            }

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
        }

        private static bool TryParseTimestamp(string value, out DateTime parsed)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                parsed = DateTime.MinValue;
                return false;
            }

            string[] formats =
            {
                "yyyy-MM-dd HH:mm:ss.fff",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy/M/d H:mm:ss",
                "yyyy/M/d H:mm",
                "yyyy-MM-ddTHH:mm:ss"
            };

            return DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                       DateTimeStyles.AssumeLocal, out parsed)
                   || DateTime.TryParse(value, CultureInfo.InvariantCulture,
                       DateTimeStyles.AssumeLocal, out parsed);
        }

        private static List<string> ParseCsvLine(string line)
        {
            List<string> cells = new List<string>();
            StringBuilder cell = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char character = line[i];
                if (character == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (character == ',' && !quoted)
                {
                    cells.Add(cell.ToString());
                    cell.Length = 0;
                }
                else
                {
                    cell.Append(character);
                }
            }

            cells.Add(cell.ToString());
            return cells;
        }
    }
}
