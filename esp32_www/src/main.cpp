// put wifi.txt on SD (M5Stack core2) or alternatively on SPIFFS (any ESP32)
// first line is SSID, next line is password

#ifdef M5STACK_CORE2
#define SPI_PORT SPI3_HOST
#define SPI_CLK 18
#define SPI_MISO 38
#define SPI_MOSI 23

#define SD_PORT SPI3_HOST
#define SD_CS 4
#endif
#include <sys/stat.h>
#include <sys/unistd.h>
#include <math.h>
#ifdef M5STACK_CORE2
#include <esp_i2c.hpp>  // i2c initialization
#include <m5core2_power.hpp>  // AXP192 power management (core2)
#endif
#include "driver/gpio.h"
#include "driver/spi_master.h"
#include "driver/uart.h"
#include "esp_http_server.h"
#include "esp_spiffs.h"
#include "esp_vfs_fat.h"
#include "esp_wifi.h"
#include "nvs_flash.h"
// these are globals we use in the page

static const float example_star_rating=3.8;

static void httpd_send_block(const char* data, size_t len, void* arg);
static void httpd_send_expr(int expr, void* arg);
static void httpd_send_expr(float expr, void* arg);
static void httpd_send_expr(const char* expr, void* arg);

#define WWW_CONTENT_IMPLEMENTATION
#include "www_content.h"
#ifdef M5STACK_CORE2
using namespace esp_idf;  // devices
#endif
static constexpr const EventBits_t wifi_connected_bit = BIT0;
static constexpr const EventBits_t wifi_fail_bit = BIT1;
static EventGroupHandle_t wifi_event_group = NULL;
static char wifi_ssid[65];
static char wifi_pass[129];
static esp_ip4_addr_t wifi_ip;

static size_t wifi_retry_count = 0;
static void wifi_event_handler(void* arg, esp_event_base_t event_base,
                               int32_t event_id, void* event_data) {
    if (event_base == WIFI_EVENT && event_id == WIFI_EVENT_STA_START) {
        esp_wifi_connect();
    } else if (event_base == WIFI_EVENT &&
               event_id == WIFI_EVENT_STA_DISCONNECTED) {
        if (wifi_retry_count < 3) {
            esp_wifi_connect();
            ++wifi_retry_count;
        } else {
            puts("wifi connection failed");
            xEventGroupSetBits(wifi_event_group, wifi_fail_bit);
        }
    } else if (event_base == IP_EVENT && event_id == IP_EVENT_STA_GOT_IP) {
        puts("got IP address");
        wifi_retry_count = 0;
        ip_event_got_ip_t* event = (ip_event_got_ip_t*)event_data;
        memcpy(&wifi_ip, &event->ip_info.ip, sizeof(wifi_ip));
        xEventGroupSetBits(wifi_event_group, wifi_connected_bit);
    }
}
static bool wifi_load(const char* path, char* ssid, char* pass) {
    FILE* file = fopen(path, "r");
    if (file != nullptr) {
        // parse the file
        fgets(ssid, 64, file);
        char* sv = strchr(ssid, '\n');
        if (sv != nullptr) *sv = '\0';
        sv = strchr(ssid, '\r');
        if (sv != nullptr) *sv = '\0';
        fgets(pass, 128, file);
        fclose(file);
        sv = strchr(pass, '\n');
        if (sv != nullptr) *sv = '\0';
        sv = strchr(pass, '\r');
        if (sv != nullptr) *sv = '\0';
        return true;
    }
    return false;
}
static void wifi_init(const char* ssid, const char* password) {
    nvs_flash_init();
    wifi_event_group = xEventGroupCreate();

    ESP_ERROR_CHECK(esp_netif_init());

    ESP_ERROR_CHECK(esp_event_loop_create_default());
    esp_netif_create_default_wifi_sta();

    wifi_init_config_t cfg = WIFI_INIT_CONFIG_DEFAULT();
    ESP_ERROR_CHECK(esp_wifi_init(&cfg));

    esp_event_handler_instance_t instance_any_id;
    esp_event_handler_instance_t instance_got_ip;
    ESP_ERROR_CHECK(esp_event_handler_instance_register(
        WIFI_EVENT, ESP_EVENT_ANY_ID, &wifi_event_handler, NULL,
        &instance_any_id));
    ESP_ERROR_CHECK(esp_event_handler_instance_register(
        IP_EVENT, IP_EVENT_STA_GOT_IP, &wifi_event_handler, NULL,
        &instance_got_ip));

    wifi_config_t wifi_config;
    memset(&wifi_config, 0, sizeof(wifi_config));
    memcpy(wifi_config.sta.ssid, ssid, strlen(ssid) + 1);
    memcpy(wifi_config.sta.password, password, strlen(password) + 1);
    wifi_config.sta.threshold.authmode = WIFI_AUTH_WPA_WPA2_PSK;
    wifi_config.sta.sae_pwe_h2e = WPA3_SAE_PWE_BOTH;
    // wifi_config.sta.sae_h2e_identifier[0]=0;
    ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_STA));
    ESP_ERROR_CHECK(esp_wifi_set_config(WIFI_IF_STA, &wifi_config));
    ESP_ERROR_CHECK(esp_wifi_start());
}
enum WIFI_STATUS { WIFI_WAITING, WIFI_CONNECTED, WIFI_CONNECT_FAILED };
static WIFI_STATUS wifi_status() {
    if (wifi_event_group == nullptr) {
        return WIFI_WAITING;
    }
    EventBits_t bits = xEventGroupGetBits(wifi_event_group) &
                       (wifi_connected_bit | wifi_fail_bit);
    if (bits == wifi_connected_bit) {
        return WIFI_CONNECTED;
    } else if (bits == wifi_fail_bit) {
        return WIFI_CONNECT_FAILED;
    }
    return WIFI_WAITING;
}

static httpd_handle_t httpd_handle = nullptr;
static SemaphoreHandle_t httpd_ui_sync = nullptr;
struct httpd_async_resp_arg {
    httpd_handle_t hd;
    int fd;
};
static void httpd_send_chunked(httpd_async_resp_arg* resp_arg,
                               const char* buffer, size_t buffer_len) {
    char buf[64];
    puts(buffer);
    httpd_handle_t hd = resp_arg->hd;
    int fd = resp_arg->fd;
    if (buffer && buffer_len) {
        itoa(buffer_len, buf, 16);
        strcat(buf, "\r\n");
        httpd_socket_send(hd, fd, buf, strlen(buf), 0);
        httpd_socket_send(hd, fd, buffer, buffer_len, 0);
        httpd_socket_send(hd, fd, "\r\n", 2, 0);
        return;
    }
    httpd_socket_send(hd, fd, "0\r\n\r\n", 5, 0);
}
static const char* httpd_crack_query(const char* url_part, char* name,
                                     char* value) {
    if (url_part == nullptr || !*url_part) return nullptr;
    const char start = *url_part;
    if (start == '&' || start == '?') {
        ++url_part;
    }
    size_t i = 0;
    char* name_cur = name;
    while (*url_part && *url_part != '=') {
        if (i < 64) {
            *name_cur++ = *url_part;
        }
        ++url_part;
        ++i;
    }
    *name_cur = '\0';
    if (!*url_part) {
        *value = '\0';
        return url_part;
    }
    ++url_part;
    i = 0;
    char* value_cur = value;
    while (*url_part && *url_part != '&' && i < 64) {
        *value_cur++ = *url_part++;
        ++i;
    }
    *value_cur = '\0';
    return url_part;
}
static void httpd_parse_url(const char* url) {
    const char* query = strchr(url, '?');
    char name[64];
    char value[64];
    if (query != nullptr) {
        while (1) {
            query = httpd_crack_query(query, name, value);
            if (!query) {
                break;
            }
            // do work
        }
    }
}
static void httpd_send_block(const char* data, size_t len, void* arg) {
    if (!data || !*data || !len) {
        return;
    }
    httpd_async_resp_arg* resp_arg = (httpd_async_resp_arg*)arg;
    httpd_socket_send(resp_arg->hd, resp_arg->fd, data, len, 0);
}
static void httpd_send_expr(int expr, void* arg) {
    httpd_async_resp_arg* resp_arg = (httpd_async_resp_arg*)arg;
    char buf[64];
    itoa(expr, buf, 10);
    httpd_send_chunked(resp_arg, buf, strlen(buf));
}
static void httpd_send_expr(float expr, void* arg) {
    httpd_async_resp_arg* resp_arg = (httpd_async_resp_arg*)arg;
    char buf[64];
    sprintf(buf,"%0.1f",expr);
    puts(buf);
    httpd_send_chunked(resp_arg, buf, strlen(buf));
}
static void httpd_send_expr(const char* expr, void* arg) {
    httpd_async_resp_arg* resp_arg = (httpd_async_resp_arg*)arg;
    if (!expr || !*expr) {
        return;
    }
    httpd_send_chunked(resp_arg, expr, strlen(expr));
}

static esp_err_t httpd_request_handler(httpd_req_t* req) {
    httpd_async_resp_arg* resp_arg =
        (httpd_async_resp_arg*)malloc(sizeof(httpd_async_resp_arg));
    if (resp_arg == nullptr) {
        return ESP_ERR_NO_MEM;
    }
    httpd_parse_url(req->uri);
    resp_arg->hd = req->handle;
    resp_arg->fd = httpd_req_to_sockfd(req);
    if (resp_arg->fd < 0) {
        return ESP_FAIL;
    }
    httpd_queue_work(req->handle, (httpd_work_fn_t)req->user_ctx, resp_arg);
    return ESP_OK;
}
static void httpd_init() {
    httpd_ui_sync = xSemaphoreCreateMutex();
    if (httpd_ui_sync == nullptr) {
        ESP_ERROR_CHECK(ESP_ERR_NO_MEM);
    }
    httpd_config_t config = HTTPD_DEFAULT_CONFIG();
    static constexpr const size_t handlers_count = sizeof(httpd_response_handlers)/sizeof(httpd_response_handler_t);
    config.max_uri_handlers = handlers_count+1;
    config.server_port = 80;
    config.max_open_sockets = (CONFIG_LWIP_MAX_SOCKETS - 3);
    ESP_ERROR_CHECK(httpd_start(&httpd_handle, &config));
    printf("Registering %s\n","/");
    httpd_uri_t handler = {.uri = "/",
        .method = HTTP_GET,
        .handler = httpd_request_handler,
        .user_ctx = (void*)httpd_response_handlers[0].handler};
    ESP_ERROR_CHECK(httpd_register_uri_handler(httpd_handle, &handler));

    for(size_t i = 0;i<handlers_count;++i) {
        printf("Registering %s\n",httpd_response_handlers[i].path);
        handler = {.uri = httpd_response_handlers[i].path_encoded,
            .method = HTTP_GET,
            .handler = httpd_request_handler,
            .user_ctx = (void*)httpd_response_handlers[i].handler};
        ESP_ERROR_CHECK(httpd_register_uri_handler(httpd_handle, &handler));
    }
}
static void httpd_end() {
    if (httpd_handle == nullptr) {
        return;
    }
    ESP_ERROR_CHECK(httpd_stop(httpd_handle));
    httpd_handle = nullptr;
    vSemaphoreDelete(httpd_ui_sync);
    httpd_ui_sync = nullptr;
}
#ifdef M5STACK_CORE2
static void power_init() {
    // for AXP192 power management
    static m5core2_power power(esp_i2c<1, 21, 22>::instance);
    // draw a little less power
    power.initialize();
    power.lcd_voltage(3.0);
}
#endif
#ifdef SPI_PORT
static void spi_init() {
    spi_bus_config_t buscfg;
    memset(&buscfg, 0, sizeof(buscfg));
    buscfg.sclk_io_num = SPI_CLK;
    buscfg.mosi_io_num = SPI_MOSI;
    buscfg.miso_io_num = SPI_MISO;
    buscfg.quadwp_io_num = -1;
    buscfg.quadhd_io_num = -1;

    buscfg.max_transfer_sz =512+8;
        
    // Initialize the SPI bus on VSPI (SPI3)
    spi_bus_initialize(SPI_PORT, &buscfg, SPI_DMA_CH_AUTO);
}
#ifdef SD_CS
static sdmmc_card_t* sd_card = nullptr;
static bool sd_init() {
    static const char mount_point[] = "/sdcard";
    esp_vfs_fat_sdmmc_mount_config_t mount_config;
    memset(&mount_config, 0, sizeof(mount_config));
    mount_config.format_if_mount_failed = false;
    mount_config.max_files = 5;
    mount_config.allocation_unit_size = 0;

    sdmmc_host_t host = SDSPI_HOST_DEFAULT();
    host.slot = SD_PORT;
    // // This initializes the slot without card detect (CD) and write
    // protect (WP)
    // // signals.
    sdspi_device_config_t slot_config;
    memset(&slot_config, 0, sizeof(slot_config));
    slot_config.host_id = (spi_host_device_t)SD_PORT;
    slot_config.gpio_cs = (gpio_num_t)SD_CS;
    slot_config.gpio_cd = SDSPI_SLOT_NO_CD;
    slot_config.gpio_wp = SDSPI_SLOT_NO_WP;
    slot_config.gpio_int = GPIO_NUM_NC;
    if (ESP_OK != esp_vfs_fat_sdspi_mount(mount_point, &host, &slot_config,
                                          &mount_config, &sd_card)) {
        return false;
    }
    return true;
}
#endif
#endif

static void spiffs_init() {
    esp_vfs_spiffs_conf_t conf;
    memset(&conf, 0, sizeof(conf));
    conf.base_path = "/spiffs";
    conf.partition_label = NULL;
    conf.max_files = 5;
    conf.format_if_mount_failed = true;
    if (ESP_OK != esp_vfs_spiffs_register(&conf)) {
        puts("Unable to initialize SPIFFS");
        while (1) vTaskDelay(5);
    }
}

static void loop();
static void loop_task(void* arg) {
    uint32_t ts = pdTICKS_TO_MS(xTaskGetTickCount());
    while (1) {
        loop();
        uint32_t ms = pdTICKS_TO_MS(xTaskGetTickCount());
        if (ms > ts + 200) {
            ms = pdTICKS_TO_MS(xTaskGetTickCount());
            vTaskDelay(5);
        }
    }
}

extern "C" void app_main() {
    printf("ESP-IDF version: %d.%d.%d\n", ESP_IDF_VERSION_MAJOR,
           ESP_IDF_VERSION_MINOR, ESP_IDF_VERSION_PATCH);
#ifdef M5STACK_CORE2
    power_init();  // do this first
#endif
#ifdef SPI_PORT
    spi_init();    // used by the SD reader
#endif
    bool loaded = false;

    wifi_ssid[0] = 0;

    wifi_pass[0] = 0;
#ifdef SPI_PORT
#ifdef SD_CS
    if (sd_init()) {
        puts("SD card found, looking for wifi.txt creds");
        loaded = wifi_load("/sdcard/wifi.txt", wifi_ssid, wifi_pass);
    }
#endif
#endif
    if (!loaded) {
        spiffs_init();
        puts("Looking for wifi.txt creds on internal flash");
        loaded = wifi_load("/spiffs/wifi.txt", wifi_ssid, wifi_pass);
    }
    if (loaded) {
        printf("Initializing WiFi connection to %s\n", wifi_ssid);
        wifi_init(wifi_ssid, wifi_pass);
    }
    
    TaskHandle_t loop_handle;
    xTaskCreate(loop_task, "loop_task", 4096, nullptr, 10, &loop_handle);
    printf("Free SRAM: %0.2fKB\n", esp_get_free_internal_heap_size() / 1024.f);
}
static void loop() {
    static bool is_connected=false;
    if (!is_connected) {  // not connected yet
        if (wifi_status() == WIFI_CONNECTED) {
            is_connected=true;
            puts("Connected");
            // initialize the web server
            puts("Starting httpd");
            httpd_init();
            // set the QR text to our website
            static char qr_text[256];
            snprintf(qr_text, sizeof(qr_text), "http://" IPSTR,
                     IP2STR(&wifi_ip));
            puts(qr_text);
            printf("Free SRAM: %0.2fKB\n",
                   esp_get_free_internal_heap_size() / 1024.f);
        }
    } else {
        if (wifi_status() == WIFI_CONNECT_FAILED) {
            // we disconnected for some reason
            puts("Disconnected");
            is_connected=false;
            httpd_end();
            wifi_retry_count = 0;
            esp_wifi_start();
            printf("Free SRAM: %0.2fKB\n",
                   esp_get_free_internal_heap_size() / 1024.f);
        }
    }
}
