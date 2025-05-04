#include <stdio.h>
#include <stdlib.h>
#include <math.h>
#include <string.h>
#include <winsock2.h>
#define HTTPD_CONTENT_IMPLEMENTATION
#include "httpd_content.h"

typedef void (*httpd_handler_func_t)(void *);

typedef struct {
    httpd_handler_func_t handler;
    char path[512];
    int length;
    int error;
    SOCKET sock;
} httpd_context_t;

#define REQUEST_SIZE 4096
#define DEFAULT_PORT 8080

enum { RQ_UNDEF, GET, POST, PUT } response_types;

static const char *DEFAULT_ERROR_404 =
    "HTTP/1.1 404 Not Found\r\nContent-Type: text/html\r\n\r\n<!DOCTYPE "
    "html><html><head><title>404 Not Found</title></head><body><h1>Not "
    "Found</h1>The requested URL was not found on this server.</body></html>";

static void httpd_get_context(SOCKET,httpd_context_t *);
static int httpd_send_response(httpd_context_t *);
static void httpd_error_live(const char *);
static void httpd_error_die(const char *);

char enc_rfc3986[256] = {0};
char enc_html5[256] = {0};

// these are globals we use in the page

const float example_star_rating = 3.8;
const char* episode_title = "Pilot";
const char* show_title = "Burn Notice";
const unsigned char episode_number = 1;
const unsigned char season_number = 1;
const char* episode_description = "While on assignment, agent Michael Westen gets a \"Burn Notice\" and becomes untouchable. Having no idea what or who triggered his demise, Michael returns to his hometown, Miami, determined to find out the reason for his sudden termination.";

int main(int argc, char **argv) {
    for (int i = 0; i < 256; i++){

        enc_rfc3986[i] = isalnum( i) || i == '~' || i == '-' || i == '.' || i == '_' ? i : 0;
        enc_html5[i] = isalnum( i) || i == '*' || i == '-' || i == '.' || i == '_' ? i : (i == ' ') ? '+' : 0;
    }
    int addr_len;
    struct sockaddr_in local, client_addr;

    SOCKET sock, msg_sock;
    WSADATA wsaData;

    if (WSAStartup(MAKEWORD(2, 2), &wsaData) == SOCKET_ERROR)
        httpd_error_die("WSAStartup()");

    // Fill in the address structure
    local.sin_family = AF_INET;
    local.sin_addr.s_addr = INADDR_ANY;
    local.sin_port = htons(DEFAULT_PORT);

    sock = socket(AF_INET, SOCK_STREAM, 0);

    if (sock == INVALID_SOCKET) httpd_error_die("socket()");

    if (bind(sock, (struct sockaddr *)&local, sizeof(local)) == SOCKET_ERROR)
        httpd_error_die("bind()");

listen_goto:

    if (listen(sock, 10) == SOCKET_ERROR) httpd_error_die("listen()");

    printf("Waiting for connection...\n");

    int count = 0;

    while(1) {
        addr_len = sizeof(client_addr);
        msg_sock = accept(sock, (struct sockaddr *)&client_addr, &addr_len);

        if (msg_sock == INVALID_SOCKET || msg_sock == -1) httpd_error_die("accept()");

        printf("Connected to %s:%d\n", inet_ntoa(client_addr.sin_addr),
               htons(client_addr.sin_port));

        httpd_context_t context;
        httpd_get_context(msg_sock,&context);
        printf("Client requested %s\n", context.path);

        if (context.length == 0) continue;

        int sent = httpd_send_response(&context);
        closesocket(msg_sock);

        if (sent == 0)
            break;
        else if (sent == -1)
            goto listen_goto;
    }

    WSACleanup();
}

static char *httpd_url_encode(char *enc, size_t size, const char *s, const char *table){
    char* result = enc;
    if(table==NULL) table = enc_rfc3986;
    for (; *s; s++){
        if (table[(int)*s]) { 
            *enc++ = table[(int)*s];
            --size;
        }
        else {
            snprintf( enc,size, "%%%02X", *s);
            while (*++enc) {
                --size;
            }
        }
        
    }
    return result;
}

void httpd_get_context(SOCKET sock,httpd_context_t* out_context ) {
    
    int len;
    char buf[REQUEST_SIZE];

    len = recv(sock, buf, sizeof(buf), 0);
    if(len==0) {
        out_context->length = len;
        out_context->sock = sock;
        out_context->error = 0;
        out_context->handler = httpd_content_500_clasp;
        return;
            
    }
    sscanf(buf, "%s %s ", out_context->path, out_context->path);
    out_context->length = len;
    out_context->sock = sock;
    out_context->error = 0;
    out_context->handler = NULL;
    int hi = httpd_response_handler_match(out_context->path);
    if(hi>-1) {
        out_context->handler = httpd_response_handlers[hi].handler;
    } else {
        out_context->handler = httpd_content_404_clasp;
    }
}

int httpd_send_response(httpd_context_t *context) {
    if (context->error || context->handler == NULL) {
        send(context->sock, DEFAULT_ERROR_404, strlen(DEFAULT_ERROR_404), 0);
        return -1;
    }
    context->handler(context);
    closesocket(context->sock);
    printf("Served path %s\n", context->path);
    return -1;
}


static void httpd_send_chunked(void *resp_arg,
                               const char *buffer, size_t buffer_len) {
    char buf[64];
    httpd_context_t* ctx = (httpd_context_t*)resp_arg;
    SOCKET sock = ctx->sock;
    if (buffer && buffer_len) {
        itoa(buffer_len, buf, 16);
        strcat(buf, "\r\n");
        send(sock, buf, strlen(buf), 0);
        send(sock, buffer, buffer_len, 0);
        send(sock, "\r\n", 2, 0);
        return;
    }
    send(sock, "0\r\n\r\n", 5, 0);
}

static void httpd_send_block(const char *data, size_t len, void *arg) {
    if (!data || !len) {
        return;
    }
    httpd_context_t* ctx = (httpd_context_t*)arg;
    SOCKET sock = ctx->sock;
    send(sock, data, len, 0);
}
static void httpd_send_expr(int expr, void *arg) {
    char buf[64];
    itoa(expr, buf, 10);
    httpd_send_chunked(arg, buf, strlen(buf));
}
static void httpd_send_expr(float expr, void* arg) {
    char buf[64] = {0};
    sprintf(buf, "%0.2f", expr);
    for(size_t i = sizeof(buf)-1;i>0;--i) {
        char ch = buf[i];
        if(ch=='0' || ch=='.') {
            buf[i]='\0'; 
            if(ch=='.') {
                break;
            }
        } else if(ch!='\0') {
             break;
        }
    }
    httpd_send_chunked(arg, buf, strlen(buf));
}
static void httpd_send_expr(unsigned char expr, void *arg) {
    char buf[64];
    sprintf(buf, "%02d", (int)expr);
    httpd_send_chunked(arg, buf, strlen(buf));
}
static void httpd_send_expr(const char *expr, void *arg) {
    if (!expr || !*expr) {
        return;
    }
    httpd_send_chunked(arg, expr, strlen(expr));
}
void httpd_error_live(const char *s) {
    fprintf(stderr, "Error: %s failed with error %d\n", s, WSAGetLastError());
    WSACleanup();
}

void httpd_error_die(const char *s) {
    httpd_error_live(s);
    exit(EXIT_FAILURE);
}