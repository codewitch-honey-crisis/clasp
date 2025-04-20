# ClASP-Tree

ClASP-Tree: The C Language website multi-content generator

ClASP-Tree is a C and C++ oriented HTTP response generator that takes a folder of input files and generates a header with method calls to the content over a socket to a browser.

Usage:
```
clasptree

Generates dynamic ClASP content and static content from a directory tree

Usage:

clasptree <input> [ <output> ] [ /block <block> ] [ /expr <expr> ] [ /state <state> ] [ /prefix <prefix> ]
    [ /prologue <prologue> ] [ /epilogue <epilogue> ] [ /handlers <handlers> ] [ /index <index> ] [ /nostatus ]

<input>        The root directory of the site. Defaults to the current directory
<output>       The output file to generate. Defaults to <stdout>
<block>        The function call to send a literal block to the client. Defaults to response_block
<expr>         The function call to send an expression to the client. Defaults to response_expr
<state>        The variable name that holds the user state to pass to the response functions. Defaults to response_state
<prefix>       The method prefix to use, if specified.
<prologue>     The file to insert into each method before any code
<epilogue>     The file to insert into each method after any code
<handlers>     Indicated wither to generate no handler entries (none), default entries (@default) or extended (extended) handlers.
        None doesn't emit any. Default emits them in accordance with their paths, plus resoving indexes based on
        <index>. Extended does this and also adds path/ trailing handlers
<index>        Generate / default handlers for files matching this wildcard. Defaults to "index.*"
/nostatus      Suppress the status headers

clasptree /?

/?             Displays this screen
```

Consider the following
```
clasptree www esp32_www\include\httpd_content.h /prefix httpd_ /epilogue httpd_epilogue.h /state resp_arg /block httpd_send_block /expr httpd_send_expr
```
This will take all the content in a folder called www, and generate a header file called httpd_content.h with it.
It specifies the prefix to use for the generated symbols, an epilogue of code to append in each method, the argument and the methods used to send data.

To understand the details of how it works see the READMEs for CLASP and ClStat, but essentially it does the following:

- For .clasp files, they are interpreted as dynamic content and generated accordingly.
- For .h files, they are copied into the input directory in a mirrored tree, and an `#include` is added in the generated code.
- For other files, it is potentially compressed and embedded as static.


In the Visual Studio demo for clasptree, this command from above generates this content based on the contents of www:

```cpp
// Generated with clasptree
// To use this file, define HTTPD_CONTENT_IMPLEMENTATION in exactly one translation unit (.c/.cpp file) before including this header.
#ifndef HTTPD_CONTENT_H
#define HTTPD_CONTENT_H

#include "httpd_application.h"

#define HTTPD_RESPONSE_HANDLER_COUNT 5
typedef struct { const char* path; const char* path_encoded; void (* handler) (void* arg); } httpd_response_handler_t;
extern httpd_response_handler_t httpd_response_handlers[5];
#ifdef __cplusplus
extern "C" {
#endif

// ./favicon.ico
void httpd_content_favicon_ico(void* resp_arg);
// ./index.clasp
void httpd_content_index_clasp(void* resp_arg);
// ./image/S01E01 Pilot.jpg
void httpd_content_image_S01E01_Pilot_jpg(void* resp_arg);
// ./style/w3.css
void httpd_content_style_w3_css(void* resp_arg);

#ifdef __cplusplus
}
#endif

#endif // HTTPD_CONTENT_H

#ifdef HTTPD_CONTENT_IMPLEMENTATION

httpd_response_handler_t httpd_response_handlers[5] = {
    { "/", "/", httpd_content_index_clasp },
    { "/favicon.ico", "/favicon.ico", httpd_content_favicon_ico },
    { "/image/S01E01 Pilot.jpg", "/image/S01E01%20Pilot.jpg", httpd_content_image_S01E01_Pilot_jpg },
    { "/index.clasp", "/index.clasp", httpd_content_index_clasp },
    { "/style/w3.css", "/style/w3.css", httpd_content_style_w3_css }
};
void httpd_content_favicon_ico(void* resp_arg) {
    // HTTP/1.1 200 OK
    // Content-Type: image/x-icon
    // Content-Encoding: deflate
    // Content-Length: 676
    // 
    static const unsigned char http_response_data[] = {
        0x48, 0x54, 0x54, 0x50, 0x2F, 0x31, 0x2E, 0x31, 0x20, 0x32, 0x30, 0x30, 0x20, 0x4F, 0x4B, 0x0D, 0x0A, 0x43, 0x6F, 0x6E, 
        0x74, 0x65, 0x6E, 0x74, ... };

    httpd_send_block((const char*)http_response_data,sizeof(http_response_data), resp_arg);
    free(resp_arg);
}
void httpd_content_index_clasp(void* resp_arg) {
    httpd_send_block("HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nTransfer-Encoding: chunked\r\n\r\nC5\r\n<!DO"
        "CTYPE html>\r\n<html>\r\n<head>\r\n    <meta charset=\"UTF-8\">\r\n    <meta name=\"viewpor"
        "t\" content=\"width=device-width, initial-scale=1\">\r\n    <link rel=\"stylesheet\" hr"
        "ef=\"./style/w3.css\">\r\n    <title>\r\n", 275, resp_arg);
    httpd_send_expr(episode_title, resp_arg);
    httpd_send_block("3\r\n - \r\n", 8, resp_arg);
    httpd_send_expr(show_title, resp_arg);
    httpd_send_block("3FB\r\n</title>\r\n    <style>\r\n        .w3-bar-block .w3-bar-item {\r\n            pa"
        "dding: 20px\r\n        }\r\n\r\n        body {\r\n            font-family: 'Segoe UI', T"
        "ahoma, Geneva, Verdana, sans-serif;\r\n        }\r\n\r\n        h3 {\r\n            font"
        "-family: 'Lucida Sans', 'Lucida Sans Regular', 'Lucida Grande', 'Lucida Sans Uni"
        "code', Geneva, Verdana, sans-serif;\r\n            font-size: larger;\r\n        }\r\n"
        "\r\n        .stars {\r\n            color: orange;\r\n        }\r\n        video {\r\n    "
        "        object-fit: contain;\r\n            max-width:1200px;\r\n            margin:"
        " auto;\r\n        }\r\n    </style>\r\n</head>\r\n<body>\r\n    <!-- Sidebar (hidden by de"
        "fault) -->\r\n    <nav class=\"w3-sidebar w3-bar-block w3-card w3-top w3-xlarge w3-"
        "animate-left\" style=\"display: none; z-index: 2; width: 40%; min-width: 300px\" id"
        "=\"mySidebar\">\r\n        <a href=\"https://github.com/codewitch-honey-crisis/clasp\""
        " onclick=\"w3_close()\" class=\"w3-bar-item w3-button\">ClASP at GitHub</a>\r\n       "
        " <a href=\"/\" onclick=\"w3_close()\" class=\"w3-bar-item w3-button\">\r\n", 1026, resp_arg);
    httpd_send_expr(episode_title, resp_arg);
    httpd_send_block("12C\r\n</a>\r\n    </nav>\r\n    <div class=\"w3-top\">\r\n        <div class=\"w3-white w3"
        "-xlarge\" style=\"max-width: 1200px; margin: auto\">\r\n            <div class=\"w3-bu"
        "tton w3-padding-16 w3-left\" onclick=\"w3_open()\">\xE2\x98\xB0</div>\r\n            <div clas"
        "s=\"w3-right w3-padding-16\">\r\n                <span class=\"stars\">\r\n", 307, resp_arg);
    
    int r = round(example_star_rating);
    int i;
    for(i = 0;i<r;++i) {
    httpd_send_block("3\r\n\xE2\x98\x85\r\n", 8, resp_arg);
    }
    for(;i<5;++i) {
    httpd_send_block("3\r\n\xE2\x98\x86\r\n", 8, resp_arg);
    }
    httpd_send_block("D\r\n</span><span>\r\n", 18, resp_arg);
    httpd_send_expr(example_star_rating, resp_arg);
    httpd_send_block("4E\r\n</span>\r\n            </div>\r\n            <div class=\"w3-center w3-padding-16"
        "\">\r\n", 84, resp_arg);
    httpd_send_expr(episode_title, resp_arg);
    httpd_send_block("3\r\n - \r\n", 8, resp_arg);
    httpd_send_expr(show_title, resp_arg);
    httpd_send_block("8F\r\n</div>\r\n        </div>\r\n    </div>\r\n    <div class=\"w3-main w3-content w3-pa"
        "dding\" style=\"max-width: 1200px; margin-top: 100px\">\r\n        <div>\r\n", 149, resp_arg);
    char tmp[256]={0};
    httpd_send_block("19\r\n\r\n            <img alt=\"S\r\n", 31, resp_arg);
    httpd_send_expr(season_number, resp_arg);
    httpd_send_block("1\r\nE\r\n", 6, resp_arg);
    httpd_send_expr(episode_number, resp_arg);
    httpd_send_block("1\r\n \r\n", 6, resp_arg);
    httpd_send_expr(episode_title, resp_arg);
    httpd_send_block("24\r\n\" style=\"width:100%;\" src=\"./image/S\r\n", 42, resp_arg);
    httpd_send_expr(season_number, resp_arg);
    httpd_send_block("1\r\nE\r\n", 6, resp_arg);
    httpd_send_expr(episode_number, resp_arg);
    httpd_send_block("3\r\n%20\r\n", 8, resp_arg);
    httpd_send_expr(httpd_url_encode(tmp,sizeof(tmp),episode_title,nullptr), resp_arg);
    httpd_send_block("8E\r\n.jpg\" /> \r\n        </div>\r\n                 \r\n        <div class=\"w3-white w"
        "3-large\" style=\"max-width: 1200px; margin: auto\">\r\n            <p>\r\n", 148, resp_arg);
    httpd_send_expr(episode_description, resp_arg);
    httpd_send_block("166\r\n</p>\r\n        </div>\r\n    </div>\r\n    <script>\r\n        // Script to open a"
        "nd close sidebar\r\n        function w3_open() {\r\n            document.getElementB"
        "yId(\"mySidebar\").style.display = \"block\";\r\n        }\r\n\r\n        function w3_clos"
        "e() {\r\n            document.getElementById(\"mySidebar\").style.display = \"none\";\r"
        "\n        }\r\n    </script>\r\n</body>\r\n</html>\r\n0\r\n\r\n", 370, resp_arg);
    free(resp_arg);
}
void httpd_content_image_S01E01_Pilot_jpg(void* resp_arg) {
    // HTTP/1.1 200 OK
    // Content-Type: image/jpeg
    // Content-Encoding: deflate
    // Content-Length: 11053
    // 
    static const unsigned char http_response_data[] = {
        0x48, 0x54, 0x54, 0x50, 0x2F, 0x31, 0x2E, 0x31, 0x20, 0x32, 0x30, 0x30, 0x20, 0x4F, 0x4B, 0x0D, 0x0A, 0x43, 0x6F, 0x6E, 
        0x74, 0x65, 0x6E, 0x74, 0x2D, 0x54, 0x79, 0x70, 0x65, 0x3A, 0x20, 0x69, 0x6D, 0x61, 0x67, 0x65, 0x2F, 0x6A, 0x70, 0x65, 
        0x67, 0x0D, 0x0A, 0x43, 0x6F, 0x6E, 0x74, 0x65, 0x6E, 0x74, 0x2D, 0x45, 0x6E, ... };
    httpd_send_block((const char*)http_response_data,sizeof(http_response_data), resp_arg);
    free(resp_arg);
}
void httpd_content_style_w3_css(void* resp_arg) {
    // HTTP/1.1 200 OK
    // Content-Type: text/css
    // Content-Encoding: deflate
    // Content-Length: 5235
    // 
    static const unsigned char http_response_data[] = {
        0x48, 0x54, 0x54, 0x50, 0x2F, 0x31, 0x2E, 0x31, 0x20, 0x32, 0x30, 0x30, 0x20, 0x4F, 0x4B, 0x0D, 0x0A, 0x43, 0x6F, 0x6E, 
        0x74, 0x65, 0x6E, 0x74, 0x2D, 0x54, 0x79, 0x70, 0x65, 0x3A, 0x20, 0x74, ... };
    httpd_send_block((const char*)http_response_data,sizeof(http_response_data), resp_arg);
    free(resp_arg);
}
#endif // HTTPD_CONTENT_IMPLEMENTATION
```
(some of the binary data omitted from above)

You'll often declare something like `httpd_application.h` and put it in your web root. Whenever ClASP-Tree encounters a .h file it will copy the header into the same relative directory structure in the output's directory, and add an `#include` to the newly copied file.
This will contain declarations used in your pages. The following is included with the demo:

```cpp
#ifndef HTTPD_APPLICATION_H
#define HTTPD_APPLICATION_H
#include <stddef.h>
#include <stdint.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>

extern const float example_star_rating; 
extern const char* episode_title; 
extern const char* show_title;
extern const unsigned char episode_number;
extern const unsigned char season_number;
extern const char* episode_description;
static void httpd_send_block(const char* data, size_t len, void* arg);
static void httpd_send_expr(int expr, void* arg);
static void httpd_send_expr(unsigned char expr, void* arg);
static void httpd_send_expr(float expr, void* arg);
static void httpd_send_expr(const char* expr, void* arg);
extern char enc_rfc3986[256];
extern char enc_html5[256];
static char* httpd_url_encode(char* enc, size_t size, const char* s, const char* table);
#endif // HTTPD_APPLICATION_H
```

This gives your dynamic pages access to the methods and fields used to generate the HTTP content.

Finally, you include it in your main application. In exactly one translation unit you must `#define XXXX_IMPLEMENTATION` where XXXX is derived from the name of the file. Look in the header itself for the actual name. You must define that before including the header in one .c or .cpp file (ie: translation unit) or it won't link. Do not use the define more than once, but you may include the header wherever you need it.

Here's the example in the demo:

```cpp
#define HTTPD_CONTENT_IMPLEMENTATION
#include "httpd_content.h"
```

This will usually be in your web server's main implementation code.

You can now access and register the handlers' paths and associated handler methods.

Here's an example of using the generated handlers array to initialize the web server (ESP-IDF/ESP32)

```cpp
static httpd_handle_t httpd_handle = NULL;
struct httpd_async_resp_arg {
    httpd_handle_t hd;
    int fd;
};
static esp_err_t httpd_request_handler(httpd_req_t* req) {
    httpd_async_resp_arg* resp_arg =
        (httpd_async_resp_arg*)malloc(sizeof(httpd_async_resp_arg));
    if (resp_arg == NULL) {
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
    httpd_config_t config = HTTPD_DEFAULT_CONFIG();
    config.max_uri_handlers = HTTPD_RESPONSE_HANDLER_COUNT;
    config.server_port = 80;
    config.max_open_sockets = (CONFIG_LWIP_MAX_SOCKETS - 3);
    ESP_ERROR_CHECK(httpd_start(&httpd_handle, &config));

    for (size_t i = 0; i < HTTPD_RESPONSE_HANDLER_COUNT; ++i) {
        printf("Registering %s\n", httpd_response_handlers[i].path);
        httpd_uri_t handler = {
            .uri = httpd_response_handlers[i].path_encoded,
            .method = HTTP_GET,
            .handler = httpd_request_handler,
            .user_ctx = (void*)httpd_response_handlers[i].handler};
        ESP_ERROR_CHECK(httpd_register_uri_handler(httpd_handle, &handler));
    }
}
```
In the command line from earlier we've included epilogue code in each handler function to `free(resp_arg);` - you'll see that code at the end of each handler function above.
