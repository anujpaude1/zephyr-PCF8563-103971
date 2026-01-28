/*
 * PCF8563 RTC Driver Bug Test - Using REAL Driver
 * Initial project setup and build configuration verified
 */

#include <zephyr/kernel.h>
#include <zephyr/device.h>
#include <zephyr/drivers/rtc.h>
#include <zephyr/logging/log.h>

LOG_MODULE_REGISTER(pcf8563_test, LOG_LEVEL_INF);

/* Get the RTC device from device tree */
#define RTC_DEV DT_NODELABEL(pcf8563)

static void test_year_with_real_driver(uint16_t actual_year)
{
    const struct device *rtc = DEVICE_DT_GET(RTC_DEV);
    
    if (!device_is_ready(rtc)) {
        LOG_ERR("RTC device not ready!");
        return;
    }
    
    LOG_INF("\n========================================");
    LOG_INF("Testing Year %d with REAL DRIVER", actual_year);
    LOG_INF("========================================");
    
    /* Prepare time to set */
    struct rtc_time set_time = {
        .tm_sec = 30,
        .tm_min = 45,
        .tm_hour = 14,
        .tm_mday = 18,
        .tm_mon = 2,    // February (0-based)
        .tm_year = actual_year - 1900,  // tm_year format
        .tm_wday = 2,   // Tuesday
    };
    
    LOG_INF("Setting time: Year %d (tm_year=%d)", 
            actual_year, set_time.tm_year);
    
    /* CALL REAL DRIVER - This executes actual buggy code! */
    int ret = rtc_set_time(rtc, &set_time);
    if (ret < 0) {
        LOG_ERR("Failed to set time: %d", ret);
        return;
    }
    
    LOG_INF("Time set successfully");
    
    /* Wait a bit */
    k_sleep(K_MSEC(100));
    
    /* Read back time - CALLS REAL DRIVER AGAIN */
    struct rtc_time get_time = {0};
    ret = rtc_get_time(rtc, &get_time);
    if (ret < 0) {
        LOG_ERR("Failed to get time: %d", ret);
        return;
    }
    
    /* Check results */
    LOG_INF("Read back: %04d-%02d-%02d %02d:%02d:%02d (tm_year=%d)",
            get_time.tm_year + 1900, get_time.tm_mon + 1, get_time.tm_mday,
            get_time.tm_hour, get_time.tm_min, get_time.tm_sec,
            get_time.tm_year);

    if (get_time.tm_year == set_time.tm_year) {
        LOG_INF("PASS: Year matches!");
    } else {
        LOG_ERR("FAIL: Year mismatch!");
        LOG_ERR("  Expected tm_year: %d (year %d)",
                set_time.tm_year, set_time.tm_year + 1900);
        LOG_ERR("  Got tm_year:      %d (year %d)",
                get_time.tm_year, get_time.tm_year + 1900);
        LOG_ERR("  Difference:       %d years",
                set_time.tm_year - get_time.tm_year);
    }
}

int main(void)
{
    LOG_INF("\n");
    LOG_INF("========================================");
    LOG_INF("  PCF8563 REAL DRIVER Bug Test");
    LOG_INF("  Testing ACTUAL Zephyr Driver Code");
    LOG_INF("========================================");
    LOG_INF("\n");
    
    /* Test with different years */
    test_year_with_real_driver(1970);
    k_sleep(K_MSEC(500));

    test_year_with_real_driver(2000);
    k_sleep(K_MSEC(500));

    test_year_with_real_driver(2026);
    k_sleep(K_MSEC(500));

    test_year_with_real_driver(2050);
    k_sleep(K_MSEC(500));

    test_year_with_real_driver(1920);
    k_sleep(K_MSEC(500));

    test_year_with_real_driver(2099);
    
    LOG_INF("\n========================================");
    LOG_INF("Test Complete - Check results above");
    LOG_INF("========================================\n");
    
    return 0;
}
