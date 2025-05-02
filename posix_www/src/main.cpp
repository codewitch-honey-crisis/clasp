#include <arpa/inet.h>          /* inet_ntoa */
#include <signal.h>
#include <dirent.h>
#include <errno.h>
#include <fcntl.h>
#include <time.h>
#include <netinet/in.h>
#include <netinet/tcp.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/sendfile.h>
#include <sys/socket.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <unistd.h>
#include <math.h>
#include <ctype.h>
#define HTTPD_CONTENT_IMPLEMENTATION
#include "httpd_content.h"

#define DEFAULT_PORT 8080
#define LISTENQ  1024  /* second argument to listen() */
#define MAXLINE 1024   /* max length of a line */
#define RIO_BUFSIZE 1024


// these are globals we use in the page

const float example_star_rating = 3.8;
const char* episode_title = "Pilot";
const char* show_title = "Burn Notice";
const unsigned char episode_number = 1;
const unsigned char season_number = 1;
const char* episode_description = "While on assignment, agent Michael Westen gets a \"Burn Notice\" and becomes untouchable. Having no idea what or who triggered his demise, Michael returns to his hometown, Miami, determined to find out the reason for his sudden termination.";


char enc_rfc3986[256] = {0};
char enc_html5[256] = {0};

typedef struct {
    int rio_fd;                 /* descriptor for this buf */
    int rio_cnt;                /* unread byte in this buf */
    char *rio_bufptr;           /* next unread byte in this buf */
    char rio_buf[RIO_BUFSIZE];  /* internal buffer */
} rio_t;

/* Simplifies calls to bind(), connect(), and accept() */
typedef struct sockaddr SA;

typedef struct {
    char path[512];
    size_t end;
} http_context_t;


void rio_readinitb(rio_t *rp, int fd){
    rp->rio_fd = fd;
    rp->rio_cnt = 0;
    rp->rio_bufptr = rp->rio_buf;
}

ssize_t writen(int fd, const void *usrbuf, size_t n){
    size_t nleft = n;
    ssize_t nwritten;
    const char *bufp = (const char*)usrbuf;

    while (nleft > 0){
        if ((nwritten = write(fd, bufp, nleft)) <= 0){
            if (errno == EINTR)  /* interrupted by sig handler return */
                nwritten = 0;    /* and call write() again */
            else
                return -1;       /* errorno set by write() */
        }
        nleft -= nwritten;
        bufp += nwritten;
    }
    return n;
}


/*
 * rio_read - This is a wrapper for the Unix read() function that
 *    transfers min(n, rio_cnt) bytes from an internal buffer to a user
 *    buffer, where n is the number of bytes requested by the user and
 *    rio_cnt is the number of unread bytes in the internal buffer. On
 *    entry, rio_read() refills the internal buffer via a call to
 *    read() if the internal buffer is empty.
 */
/* $begin rio_read */
static ssize_t rio_read(rio_t *rp, char *usrbuf, size_t n){
    int cnt;
    while (rp->rio_cnt <= 0){  /* refill if buf is empty */

        rp->rio_cnt = read(rp->rio_fd, rp->rio_buf,
                           sizeof(rp->rio_buf));
        if (rp->rio_cnt < 0){
            if (errno != EINTR) /* interrupted by sig handler return */
                return -1;
        }
        else if (rp->rio_cnt == 0)  /* EOF */
            return 0;
        else
            rp->rio_bufptr = rp->rio_buf; /* reset buffer ptr */
    }

    /* Copy min(n, rp->rio_cnt) bytes from internal buf to user buf */
    cnt = n;
    if (rp->rio_cnt < n)
        cnt = rp->rio_cnt;
    memcpy(usrbuf, rp->rio_bufptr, cnt);
    rp->rio_bufptr += cnt;
    rp->rio_cnt -= cnt;
    return cnt;
}

/*
 * rio_readlineb - robustly read a text line (buffered)
 */
ssize_t rio_readlineb(rio_t *rp, void *usrbuf, size_t maxlen){
    int n, rc;
    char c, *bufp = (char*)usrbuf;

    for (n = 1; n < maxlen; n++){
        if ((rc = rio_read(rp, &c, 1)) == 1){
            *bufp++ = c;
            if (c == '\n')
                break;
        } else if (rc == 0){
            if (n == 1)
                return 0; /* EOF, no data read */
            else
                break;    /* EOF, some data was read */
        } else
            return -1;    /* error */
    }
    *bufp = 0;
    return n;
}

int open_listenfd(int port){
    int listenfd, optval=1;
    struct sockaddr_in serveraddr;

    /* Create a socket descriptor */
    if ((listenfd = socket(AF_INET, SOCK_STREAM, 0)) < 0)
        return -1;

    /* Eliminates "Address already in use" error from bind. */
    if (setsockopt(listenfd, SOL_SOCKET, SO_REUSEADDR,
                   (const void *)&optval , sizeof(int)) < 0)
        return -1;

    // 6 is TCP's protocol number
    // enable this, much faster : 4000 req/s -> 17000 req/s
    if (setsockopt(listenfd, 6, TCP_CORK,
                   (const void *)&optval , sizeof(int)) < 0)
        return -1;

    /* Listenfd will be an endpoint for all requests to port
       on any IP address for this host */
    memset(&serveraddr, 0, sizeof(serveraddr));
    serveraddr.sin_family = AF_INET;
    serveraddr.sin_addr.s_addr = htonl(INADDR_ANY);
    serveraddr.sin_port = htons((unsigned short)port);
    if (bind(listenfd, (SA *)&serveraddr, sizeof(serveraddr)) < 0)
        return -1;

    /* Make it a listening socket ready to accept connection requests */
    if (listen(listenfd, LISTENQ) < 0)
        return -1;
    return listenfd;
}

void parse_request(int fd, http_context_t *req){
    rio_t rio;
    char buf[MAXLINE], method[MAXLINE], uri[MAXLINE];
    req->end = 0;              /* default */

    rio_readinitb(&rio, fd);
    rio_readlineb(&rio, buf, MAXLINE);
    sscanf(buf, "%s %s", method, uri); /* version is not cared */
    /* read all */
    while(buf[0] != '\n' && buf[1] != '\n') { /* \n || \r\n */
        rio_readlineb(&rio, buf, MAXLINE);
    }
    if(uri[0] == '/'){
        int length = strlen(uri);
        for (int i = 0; i < length; ++ i) {
            if (uri[i] == '?') {
                req->path[i] = '\0';
                break;
            } else {
                req->path[i]=uri[i];
            }
        }
        req->path[length]='\0';
    } else {
        *req->path=0;
    }
    
}

void client_error(int fd, int status, const char *msg, const char *longmsg){
    char buf[MAXLINE];
    sprintf(buf, "HTTP/1.1 %d %s\r\n", status, msg);
    sprintf(buf + strlen(buf),
            "Content-length: %lu\r\n\r\n", strlen(longmsg));
    sprintf(buf + strlen(buf), "%s", longmsg);
    writen(fd, buf, strlen(buf));
}

void process(int fd, struct sockaddr_in *clientaddr){
    http_context_t req;
    parse_request(fd, &req);
    struct stat sbuf;
    int hi = httpd_response_handler_match(req.path);
    if(hi>-1) {
        httpd_response_handler_t* h = &httpd_response_handlers[hi];
        h->handler(&fd);
        return;
    }
    httpd_content_404_clasp(&fd);
}


static void httpd_send_block(const char *data, size_t len, void *arg) {
    if (!data || !*data || !len) {
        return;
    }
    int* pfd = (int*)arg;
    writen(*pfd,data,len);
}

static void httpd_send_chunked(void *arg,
                               const char *buffer, size_t buffer_len) {
    char buf[64];
    if (buffer && buffer_len) {
        snprintf(buf,sizeof(buf),"%X\r\n", (unsigned int)buffer_len);
        httpd_send_block(buf,strlen(buf),arg);
        httpd_send_block(buffer,buffer_len,arg);
        httpd_send_block("\r\n",2,arg);
        return;
    }
    httpd_send_block("0\r\n\r\n", 5, arg);
}
static void httpd_send_expr(int expr, void *arg) {
    char buf[64];
    snprintf(buf,sizeof(buf),"%d",expr);
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
int main(int argc, char** argv){
    for (int i = 0; i < 256; i++){

        enc_rfc3986[i] = isalnum( i) || i == '~' || i == '-' || i == '.' || i == '_' ? i : 0;
        enc_html5[i] = isalnum( i) || i == '*' || i == '-' || i == '.' || i == '_' ? i : (i == ' ') ? '+' : 0;
    }
    struct sockaddr_in clientaddr;
    int default_port = DEFAULT_PORT,
        listenfd,
        connfd;
    char buf[256];
    char *path = getcwd(buf, 256);
    socklen_t clientlen = sizeof(clientaddr);

    listenfd = open_listenfd(default_port);
    if (listenfd > 0) {
        printf("waiting port %d, fd is %d\n", default_port, listenfd);
    } else {
        perror("socket error");
        exit(listenfd);
    }
    // Ignore SIGPIPE signal, so if browser cancels the request, it
    // won't kill the whole process.
    signal(SIGPIPE, SIG_IGN);

    for(int i = 0; i < 10; i++) {
        int pid = fork();
        if (pid == 0) {         //  child
            while(1){
                connfd = accept(listenfd, (SA *)&clientaddr, &clientlen);
                process(connfd, &clientaddr);
                close(connfd);
            }
        } else if (pid > 0) {   //  parent
            printf("child pid is %d\n", pid);
        } else {
            perror("fork");
        }
    }

    while(1){
        connfd = accept(listenfd, (SA *)&clientaddr, &clientlen);
        process(connfd, &clientaddr);
        close(connfd);
    }

    return 0;
}

