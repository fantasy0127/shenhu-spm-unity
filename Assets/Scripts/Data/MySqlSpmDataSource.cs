using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

#if SHENHU_MYSQL
using MySql.Data.MySqlClient;
#endif

namespace ShenhuSpm
{
    [Serializable]
    public sealed class MySqlConnectionSettings
    {
        public string Server = "127.0.0.1";
        public uint Port = 3306;
        public string Database = "shenhu_spm";
        public string UserId = "replace_me";
        public string Password = "replace_me";
        public string Table = "spm_observations";
        public string StationColumn = "station";
        public string TimestampColumn = "timestamp";
        public string ElapsedHoursColumn = "elapsed_hours";
        public string TurbidityColumn = "turbidity_ntu";
        public string SpmColumn = "spm_mg_l";
    }

    public sealed class MySqlSpmDataSource : ISpmDataSource
    {
        private readonly MySqlConnectionSettings settings;
        private readonly SpmStation station;

        public MySqlSpmDataSource(MySqlConnectionSettings settings, SpmStation station)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            this.settings = settings;
            this.station = station;
        }

        public List<SpmRecord> Load()
        {
#if SHENHU_MYSQL
            ValidateIdentifiers(settings);

            MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder();
            builder.Server = settings.Server;
            builder.Port = settings.Port;
            builder.Database = settings.Database;
            builder.UserID = settings.UserId;
            builder.Password = settings.Password;
            builder.SslMode = MySqlSslMode.Preferred;

            string query = string.Format(
                "SELECT `{0}`, `{1}`, `{2}`, `{3}`, `{4}` FROM `{5}` WHERE `{0}` = @station ORDER BY `{2}`, `{1}`",
                settings.StationColumn,
                settings.TimestampColumn,
                settings.ElapsedHoursColumn,
                settings.TurbidityColumn,
                settings.SpmColumn,
                settings.Table);

            List<SpmRecord> records = new List<SpmRecord>();
            using (MySqlConnection connection = new MySqlConnection(builder.ConnectionString))
            using (MySqlCommand command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@station", station.ToString());
                connection.Open();
                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    int timestampOrdinal = reader.GetOrdinal(settings.TimestampColumn);
                    int elapsedOrdinal = reader.GetOrdinal(settings.ElapsedHoursColumn);
                    int turbidityOrdinal = reader.GetOrdinal(settings.TurbidityColumn);
                    int spmOrdinal = reader.GetOrdinal(settings.SpmColumn);
                    while (reader.Read())
                    {
                        records.Add(new SpmRecord
                        {
                            Station = station,
                            Timestamp = reader.GetDateTime(timestampOrdinal),
                            ElapsedHours = reader.GetDouble(elapsedOrdinal),
                            TurbidityNtu = reader.IsDBNull(turbidityOrdinal)
                                ? double.NaN
                                : reader.GetDouble(turbidityOrdinal),
                            SpmMgPerL = reader.IsDBNull(spmOrdinal)
                                ? double.NaN
                                : reader.GetDouble(spmOrdinal)
                        });
                    }
                }
            }

            return records;
#else
            throw new NotSupportedException(
                "MySQL support is disabled. Install MySql.Data and add SHENHU_MYSQL to Player Settings > Scripting Define Symbols. " +
                "The CSV quick test does not require MySQL.");
#endif
        }

        private static void ValidateIdentifiers(MySqlConnectionSettings value)
        {
            string[] identifiers =
            {
                value.Table,
                value.StationColumn,
                value.TimestampColumn,
                value.ElapsedHoursColumn,
                value.TurbidityColumn,
                value.SpmColumn
            };

            foreach (string identifier in identifiers)
            {
                if (string.IsNullOrWhiteSpace(identifier) || !Regex.IsMatch(identifier, "^[A-Za-z0-9_]+$"))
                {
                    throw new ArgumentException("Unsafe MySQL table or column identifier: " + identifier);
                }
            }
        }
    }
}
