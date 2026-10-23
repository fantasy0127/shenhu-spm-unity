CREATE TABLE IF NOT EXISTS spm_observations (
    station VARCHAR(8) NOT NULL,
    timestamp DATETIME(3) NOT NULL,
    elapsed_hours DOUBLE NOT NULL,
    turbidity_ntu DOUBLE NULL,
    spm_mg_l DOUBLE NULL,
    PRIMARY KEY (station, timestamp, elapsed_hours),
    INDEX idx_spm_station_elapsed (station, elapsed_hours)
);
