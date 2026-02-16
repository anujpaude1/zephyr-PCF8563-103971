# PCF8563 RTC Driver Bug Test — Zephyr RTOS

A Zephyr RTOS application that reproduces and demonstrates a year-encoding bug in the PCF8563 real-time clock driver. The project uses the [Renode](https://renode.io/) simulator with a custom C# peripheral model to run the test entirely in software without physical hardware.

---

## Background

The NXP PCF8563 is a low-power I2C RTC that stores the year as a 2-digit BCD value in the range `0x00`–`0x99`, representing years 2000–2099.

The Zephyr driver incorrectly encodes the year field:

| | Code | Example (year 2026) |
|---|---|---|
| **Correct** | `bin2bcd(tm_year % 100)` | `bin2bcd(126 % 100)` → `0x26` ✓ |
| **Buggy** | `bin2bcd(tm_year)` | `bin2bcd(126)` → `0x7E` ✗ |

`tm_year` follows the C convention of years-since-1900, so for year 2026, `tm_year = 126`. Without the `% 100`, the driver passes `126` (decimal) to `bin2bcd`, producing `0x7E` — an invalid BCD value that the real chip rejects entirely.

---

## Project Structure

```
.
├── src/
│   └── main.c                    # Test application
├── boards/
│   └── nrf52840dk_nrf52840.overlay  # Device tree overlay (I2C + PCF8563)
├── PCF8563.cs                    # Renode custom peripheral (BCD validation)
├── test_real_driver.resc         # Renode simulation script
├── CMakeLists.txt
└── prj.conf                      # Zephyr project config
```

---

## Hardware / Simulation Target

| Item | Value |
|---|---|
| Board | Nordic nRF52840 DK (`nrf52840dk_nrf52840`) |
| RTC | NXP PCF8563 on I2C bus 0, address `0x51` |
| I2C speed | 100 kHz (standard mode) |
| Simulator | Renode |

---

## How It Works

### `src/main.c`

The test application calls `test_year_with_real_driver()` for a series of years, each time:

1. Builds a `struct rtc_time` with the target year.
2. Calls `rtc_set_time()` — this invokes the real Zephyr PCF8563 driver code.
3. Waits 100 ms, then calls `rtc_get_time()` to read back the value.
4. Compares set vs read-back `tm_year`. A mismatch confirms the bug.
5. Increments global pass/fail counters and prints a final summary.

Years tested:

| Year | `tm_year` | Expected BCD | Driver sends | Result |
|------|-----------|-------------|--------------|--------|
| 1970 | 70 | `0x70` | `0x70` | PASS |
| 2000 | 100 | `0x00` | `0x64` | **FAIL** |
| 2026 | 126 | `0x26` | `0x7E` | **FAIL** |
| 2050 | 150 | `0x50` | `0x96` | **FAIL** |
| 2099 | 199 | `0x99` | invalid | **FAIL** |

### `PCF8563.cs` — Renode Peripheral Model

A C# class implementing the `II2CPeripheral` interface for Renode. It strictly validates all writes to the year register (`0x08`):

- Checks that the written byte is valid BCD (both nibbles ≤ 9).
- Logs a detailed `*** BUG DETECTED! ***` message when an invalid value is written.
- Rejects the write and stores `0x00` to simulate real-chip rejection behavior.

### `test_real_driver.resc` — Renode Script

Loads the custom peripheral, attaches it to the simulated nRF52840's TWI0 bus at address `0x51`, loads the compiled Zephyr ELF, and starts the simulation.

---

## Prerequisites

- [Zephyr SDK](https://docs.zephyrproject.org/latest/develop/getting_started/index.html) and `west` tool
- Zephyr workspace initialized at `$ZEPHYR_BASE`
- [Renode](https://renode.io/) installed and on `$PATH`

---

## Build

```bash
west build -b nrf52840dk_nrf52840
```

---

## Run in Renode Simulator

```bash
renode test_real_driver.resc
```

The UART analyzer window will show live output. A passing test prints:

```
[INF] PASS: Year matches!
```

A failing test prints:

```
[ERR] FAIL: Year mismatch!
[ERR]   Expected tm_year: 126 (year 2026)
[ERR]   Got tm_year:      0 (year 1900)
[ERR]   Difference:       126 years
```

At the end of the run a summary is printed:

```
========================================
Test Complete - Check results above
  PASSED: 1  FAILED: 4
========================================
```

---

## Configuration

Key options in `prj.conf`:

| Config | Value | Purpose |
|--------|-------|---------|
| `CONFIG_I2C` | `y` | I2C bus driver |
| `CONFIG_RTC` | `y` | RTC subsystem |
| `CONFIG_LOG_DEFAULT_LEVEL` | `3` | Info-level logging |
| `CONFIG_LOG_BUFFER_SIZE` | `4096` | Increased buffer for verbose output |
| `CONFIG_SHELL` | `y` | Serial shell for interactive debugging |

---

## Root Cause

In `drivers/rtc/rtc_pcf8563.c`, the year is encoded as:

```c
/* Buggy */
regs[PCF8563_REG_YEAR] = bin2bcd(time->tm_year);
```

The fix is:

```c
/* Correct */
regs[PCF8563_REG_YEAR] = bin2bcd(time->tm_year % 100);
```

The same off-by-100 error applies symmetrically to the decode path (`rtc_pcf8563_get`), which must add the century back:

```c
time->tm_year = bcd2bin(regs[PCF8563_REG_YEAR]) + 100; /* years since 1900, in 2000s */
```

---

## Team

| Name | Role |
|------|------|
| Ayushma Pudasaini | Initial driver research and project setup |
| Anuj Paudel | Build configuration and project scaffolding |
| Aman Bagale | Test development, logging improvements, bug analysis |

---

## License

For educational and research purposes. Zephyr RTOS is Apache 2.0 licensed.
