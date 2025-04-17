# ClASP

ClASP: The C Language ASP generator

ClASP is a C and C++ oriented HTTP response generator that takes simple ASP-like `<%`, `<%=` and `%>` syntax and generates chunk strings to send over a socket to a browser.

Usage:
```
clasp

Generates C code from ASPish pages for use with embedded web servers

Usage:

clasp <inputfile> [ <outputfile> ] [ /block <block> ] [ /expr <expr> ] [ /state <state> ] [ /method <method> ]

<inputfile>      The input file
<outputfile>     The output file. Defaults to <stdout>
<block>          The function call to send a literal block to the client. Defaults to response_block
<expr>           The function call to send an expression to the client. Defaults to response_expr
<state>          The variable name that holds the user state to pass to the response functions. Defaults to response_state
<method>         The method to wrap the code in, if specified.

clasp /?

/?               Displays this screen
```

Consider the following input document. It is very much like old style ASP, but the code is in C/++ rather than VBScript or JScript:

```html
<%@status code="200" text="OK"%>
<%@header name="Content-Type" value="text/html"%><!DOCTYPE html>
<html>
    <head>
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <title>Alarm Control Panel</title>
    </head>
    <body>
        <form method="get" action="."><%for(size_t i = 0;i<alarm_count;++i) {
            %>
            <label><%=i+1%></label><input name="a" type="checkbox" value="<%=i%>" <%if(alarm_values[i]){%>checked<%}%> /><br /><%
}%>
            <input type="submit" name="set" value="set" />
            <input type="submit" name="refresh" value="get" />
        </form>
    </body>
</html>
```

Executing clasp with the following command arguments: `demo.clasp /state resp_arg /block httpd_send_block /expr httpd_send_expr` will yield this output:

```cpp
httpd_send_block("HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nTransfer-Encoding: chunked\r\n\r\nE2\r\n<!DOCTYPE html>\r\n<html>\r\n    <head>\r\n        <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\r\n        <title>Alarm Control Panel</title>\r\n    </head>\r\n    <body>\r\n        <form method=\"get\" action=\".\">\r\n", 304, resp_arg);
for(size_t i = 0;i<alarm_count;++i) {

httpd_send_block("15\r\n\r\n            <label>\r\n", 27, resp_arg);
httpd_send_expr(i+1, resp_arg);
httpd_send_block("2F\r\n</label><input name=\"a\" type=\"checkbox\" value=\"\r\n", 53, resp_arg);
httpd_send_expr(i, resp_arg);
httpd_send_block("2\r\n\" \r\n", 7, resp_arg);
if(alarm_values[i]){
httpd_send_block("7\r\nchecked\r\n", 12, resp_arg);
}
httpd_send_block("9\r\n /><br />\r\n", 14, resp_arg);

}
httpd_send_block("A5\r\n\r\n            <input type=\"submit\" name=\"set\" value=\"set\" />\r\n            <input type=\"submit\" name=\"refresh\" value=\"get\" />\r\n        </form>\r\n    </body>\r\n</html>\r\n\r\n", 171, resp_arg);
httpd_send_block("0\r\n\r\n", 5, resp_arg);
```

You then write the simple wrapper functions from above to send data out on a socket. To implement the expression one, you will have to implement a send chunked method yourself. For normal response blocks, the chunking is part of the string, so it can just be sent.

And example of using it is here: https://github.com/codewitch-honey-crisis/core2_alarm/blob/main/src-esp-idf/control-esp-idf.cpp

## Directives

- `@status` - if inidicated, emits an HTTP status line at the top of the content - arguments are `code` and `text`. Optionally you can specify `auto-headers="false"` to disable the generation of Content-Length or Transfer-Encoding headers
- `@header` - adds an HTTP header to the output. arguments are `name` and `value`

If either of these directives are present at least part of an HTTP header is generated (with or without the status line depending on `@status`)

If `auto-headers` is enabled (which it is by default) and either headers or a status is specified then static content will get a `Content-Length` header and dynamic content will get `Transfer-Encoding: chunked` 

## Static vs Dynamic content encoding

If the page has code or expression segments in it, the entire output will be transformed into HTTP chunked format. Otherwise no transformation occurs

## Implementation errata

Note that sending multiple different types of expressions requires the ability to do method overloading in your wrappers, so `<%= ... %>` can only handle a single type of data, otherwise it's C++ only.

As mentioned, you'll probably need some sort of method to send chunked data over a socket.
Here's an example for the ESP-IDF. You'll use this with the expr method to convert expressions in `<%=` `%>` to strings and send them over the wire.

```cpp
static void httpd_send_chunked(httpd_async_resp_arg* resp_arg,
                               const char* buffer, size_t buffer_len) {
    char buf[64];
    httpd_handle_t hd = resp_arg->hd;
    int fd = resp_arg->fd;
    itoa(buffer_len, buf, 16);
    strcat(buf, "\r\n");
    httpd_socket_send(hd, fd, buf, strlen(buf), 0);
    if (buffer && buffer_len) {
        httpd_socket_send(hd, fd, buffer, buffer_len, 0);
    }
    httpd_socket_send(hd, fd, "\r\n", 2, 0);
}
```

Here's an example of using it from your code (again, ESP-IDF example) including much of the supporting infrastructure 

```cpp

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
static void httpd_parse_url_and_apply_args(const char* url) {
    const char* query = strchr(url, '?');
    bool has_set = false;
    char name[64];
    char value[64];
    if (query != nullptr) {
        while (1) {
            query = httpd_crack_query(query, name, value);
            if (!query) {
                break;
            }
            ... // do work here
        }
    }
    // check results of work from above
}
// this just sends straight to a socket with no transformation
static void httpd_send_block(const char* data, size_t len, void* arg) {
    if (!data || !*data || !len) {
        return;
    }
    httpd_async_resp_arg* resp_arg = (httpd_async_resp_arg*)arg;
    httpd_socket_send(resp_arg->hd, resp_arg->fd, data, len, 0);
}
// these expr functions turn an expr arg into a string and send it chunked
static void httpd_send_expr(int expr, void* arg) {
    httpd_async_resp_arg* resp_arg = (httpd_async_resp_arg*)arg;
    char buf[64];
    itoa(expr, buf, 10);
    httpd_send_chunked(resp_arg, buf, strlen(buf));
}
static void httpd_send_expr(const char* expr, void* arg) {
    httpd_async_resp_arg* resp_arg = (httpd_async_resp_arg*)arg;
    if (!expr || !*expr) {
        return;
    }
    httpd_send_chunked(resp_arg, expr, strlen(expr));
}
// serve the page
static void httpd_page_async_handler(void* arg) {
    httpd_async_resp_arg* resp_arg = (httpd_async_resp_arg*)arg;
    // this include file is output that was generated by ClASP:
    #include "httpd_page.h"
    free(arg);
}
// serve the API
static void httpd_api_async_handler(void* arg) {
    httpd_async_resp_arg* resp_arg = (httpd_async_resp_arg*)arg;
    // this include file is output that was generated by ClASP:
    #include "httpd_api.h"
    free(arg);
}
// dispatcher
static esp_err_t httpd_request_handler(httpd_req_t* req) {
    httpd_async_resp_arg* resp_arg =
        (httpd_async_resp_arg*)malloc(sizeof(httpd_async_resp_arg));
    if (resp_arg == nullptr) {
        return ESP_ERR_NO_MEM;
    }
    httpd_parse_url_and_apply_args(req->uri);
    resp_arg->hd = req->handle;
    resp_arg->fd = httpd_req_to_sockfd(req);
    if (resp_arg->fd < 0) {
        return ESP_FAIL;
    }
    httpd_queue_work(req->handle, (httpd_work_fn_t)req->user_ctx, resp_arg);
    return ESP_OK;
}
// initialization
static void httpd_init() {
    httpd_ui_sync = xSemaphoreCreateMutex();
    if (httpd_ui_sync == nullptr) {
        ESP_ERROR_CHECK(ESP_ERR_NO_MEM);
    }
    httpd_config_t config = HTTPD_DEFAULT_CONFIG();
    /* Modify this setting to match the number of test URI handlers */
    config.max_uri_handlers = 5;
    config.server_port = 80;
    config.max_open_sockets = (CONFIG_LWIP_MAX_SOCKETS - 3);
    ESP_ERROR_CHECK(httpd_start(&httpd_handle, &config));
    httpd_uri_t handler = {.uri = "/",
                           .method = HTTP_GET,
                           .handler = httpd_request_handler,
                           .user_ctx = (void*)httpd_page_async_handler};
    ESP_ERROR_CHECK(httpd_register_uri_handler(httpd_handle, &handler));
    handler = {.uri = "/api",
               .method = HTTP_GET,
               .handler = httpd_request_handler,
               .user_ctx = (void*)httpd_api_async_handler};
    ESP_ERROR_CHECK(httpd_register_uri_handler(httpd_handle, &handler));
}
// tear down
static void httpd_end() {
    if (httpd_handle == nullptr) {
        return;
    }
    ESP_ERROR_CHECK(httpd_stop(httpd_handle));
    httpd_handle = nullptr;
    vSemaphoreDelete(httpd_ui_sync);
    httpd_ui_sync = nullptr;
}

```