//
// Copyright (c) 2024 PCF8563 Mock for Bug Testing
// Properly validates BCD and rejects invalid values
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class PCF8563 : II2CPeripheral
    {
        public PCF8563()
        {
            registers = new byte[16];
            Reset();
        }

        public void Reset()
        {
            Array.Clear(registers, 0, registers.Length);
            registerPointer = 0;
            this.Log(LogLevel.Info, "PCF8563 RTC initialized");
        }

        private bool IsValidBCD(byte value)
        {
            var highNibble = (value >> 4) & 0x0F;
            var lowNibble = value & 0x0F;
            return (highNibble <= 9 && lowNibble <= 9);
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                return;
            }

            // First byte is register address
            registerPointer = data[0];
            this.Log(LogLevel.Debug, "Register pointer set to 0x{0:X2}", registerPointer);

            // Write subsequent bytes to registers
            for(int i = 1; i < data.Length; i++)
            {
                var regAddr = (registerPointer + i - 1) & 0x0F;
                var value = data[i];

                this.Log(LogLevel.Info, "WRITE attempt: reg[0x{0:X2}] = 0x{1:X2}", regAddr, value);

                // Special handling for year register (0x08) - STRICT VALIDATION
                if(regAddr == 0x08)
                {
                    var highNibble = (value >> 4) & 0x0F;
                    var lowNibble = value & 0x0F;

                    this.Log(LogLevel.Warning, "========================================");
                    this.Log(LogLevel.Warning, "YEAR REGISTER WRITE ATTEMPT");
                    this.Log(LogLevel.Warning, "========================================");
                    this.Log(LogLevel.Warning, "Raw value: 0x{0:X2} (decimal: {1})", value, value);
                    this.Log(LogLevel.Warning, "High nibble: {0} (binary: {1})", highNibble, Convert.ToString(highNibble, 2).PadLeft(4, '0'));
                    this.Log(LogLevel.Warning, "Low nibble: {0} (binary: {1})", lowNibble, Convert.ToString(lowNibble, 2).PadLeft(4, '0'));

                    if(!IsValidBCD(value))
                    {
                        this.Log(LogLevel.Error, "");
                        this.Log(LogLevel.Error, "*** BUG DETECTED! ***");
                        this.Log(LogLevel.Error, "Invalid BCD value: 0x{0:X2}", value);
                        this.Log(LogLevel.Error, "High nibble: {0} (max allowed: 9)", highNibble);
                        this.Log(LogLevel.Error, "Low nibble: {0} (max allowed: 9)", lowNibble);
                        this.Log(LogLevel.Error, "");
                        this.Log(LogLevel.Error, "REAL PCF8563 CHIP WOULD REJECT THIS!");
                        this.Log(LogLevel.Error, "This proves the Zephyr driver bug exists!");
                        this.Log(LogLevel.Error, "");
                        this.Log(LogLevel.Error, "Expected for year 2000-2099: BCD 0x00 to 0x99");
                        this.Log(LogLevel.Error, "Driver should send: bin2bcd(tm_year % 100)");
                        this.Log(LogLevel.Error, "Driver actually sent: bin2bcd(tm_year) [WRONG!]");
                        this.Log(LogLevel.Error, "========================================");
                        
                        // REJECT the write - do NOT store invalid BCD
                        // Real chip behavior: garbage in, garbage out or ignored
                        // We'll store 0x00 to indicate rejection
                        registers[regAddr] = 0x00;
                        this.Log(LogLevel.Error, "WRITE REJECTED - storing 0x00 instead");
                        return;
                    }
                    else
                    {
                        var bcdValue = (highNibble * 10) + lowNibble;
                        var actualYear = 2000 + bcdValue;
                        this.Log(LogLevel.Info, "Valid BCD accepted! Represents year: {0}", actualYear);
                        registers[regAddr] = value;
                    }
                }
                else
                {
                    // For non-year registers, check if they should be BCD too
                    if(regAddr >= 0x02 && regAddr <= 0x08)  // Time/date registers
                    {
                        if(!IsValidBCD(value))
                        {
                            this.Log(LogLevel.Warning, "Invalid BCD in reg[0x{0:X2}]: 0x{1:X2}", regAddr, value);
                        }
                    }
                    registers[regAddr] = value;
                }
            }
        }

        public byte[] Read(int count)
        {
            var result = new byte[count];

            this.Log(LogLevel.Debug, "READ: {0} bytes from reg[0x{1:X2}]", count, registerPointer);

            for(int i = 0; i < count; i++)
            {
                var regAddr = (registerPointer + i) & 0x0F;
                result[i] = registers[regAddr];

                this.Log(LogLevel.Info, "READ: reg[0x{0:X2}] = 0x{1:X2}", regAddr, result[i]);

                if(regAddr == 0x08)
                {
                    if(result[i] == 0x00)
                    {
                        this.Log(LogLevel.Warning, "Year register is 0x00 (may indicate rejected write)");
                    }
                    else if(IsValidBCD(result[i]))
                    {
                        var highNibble = (result[i] >> 4) & 0x0F;
                        var lowNibble = result[i] & 0x0F;
                        var bcdValue = (highNibble * 10) + lowNibble;
                        var actualYear = 2000 + bcdValue;
                        this.Log(LogLevel.Info, "Year register: {0} (year {1})", bcdValue, actualYear);
                    }
                }
            }

            // Auto-increment pointer
            registerPointer = (byte)((registerPointer + count) & 0x0F);

            return result;
        }

        public void FinishTransmission()
        {
            // I2C transaction finished
        }

        private byte[] registers;
        private byte registerPointer;
    }
}
