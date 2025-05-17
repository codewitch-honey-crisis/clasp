# ClASP Suite

1. [Introducing the ClASP suite](#1.0)
    - 1.1 [Examples](#1.1)

2. [Creating dynamic content with `.clasp` documents using the `clasp` executable](#2.0)
    - 2.1 [Concepts](#2.1)
    - 2.2 [ClASP document syntax and directives](#2.2)
    - 2.3 [ClASP Command Line Interface](#2.3)
    - 2.4 [Using ClASP generated code](#2.4)

3. [Embedding static documents with the `clstat` executable](#3.0)
    - 3.1 [Concepts](#3.1)
    - 3.2 [ClStat Command Line Interface](#3.2)
    - 3.3 [Using ClStat generated code](#3.3)

4. [Embedding an entire website using the `clasptree` executable](#4.0)
    - 4.1 [Concepts](#4.1)
        - 4.1.1 [Static and dynamic content files](#4.1.1)
        - 4.1.2 [URL response handler mappings](#4.1.2)
        - 4.1.3 [URL response handler FSM matching](#4.1.3)
        - 4.1.4 [Map files](#4.1.4)
        - 4.1.5 [Additional includes](#4.1.5)
        - 4.1.6 [Prologue and epilogue files](#4.1.6)
    - 4.2 [ClASP-Tree Command Line Interface](#4.2)
    - 4.3 [Using the generated header file](#4.3)


<a name="1.0"></a>
## Introducing the ClASP Suite

The ClASP Suite is a collection of 3 tools which can be used to embed web content directly into C/++ code, ready to be sent over an HTTP transport, like a TCP socket, optionally including the status line and computed headers. It can also handle dynamic content in `.clasp` documents for producing efficient data driven content that can be delivered from a C/++ application or web server.

The suite was developed with an eye toward embedded web servers such as those on ESP32 devices, but there are example projects for win32 and posix as well as ESP32s to illustrate that the generated code is truly cross platform.

The end goal was to have a set of easy to use tools that can allow someone to quickly develop and efficiently embed web content - including dynamic content - into C/++ code.

<a name="1.1"></a>
## Examples

The most practical way to get started is with ClASP-Tree otherwise known as the clasptree executable. This application allows you to develop all your web content underneath a folder and then embed all of that content into C/++ code that can deliver it over some kind of transport.

Sometimes an example is worth reams of documentation. To that end, several examples are provided, including 3 entire web server projects, 1 for each of 3 include platforms (posix/win32/esp32), in [`./posix_www`](https://github.com/codewitch-honey-crisis/clasp/tree/master/posix_www), [`./win32_www`](https://github.com/codewitch-honey-crisis/clasp/tree/master/win32_www) and [`./esp32_www`](https://github.com/codewitch-honey-crisis/clasp/tree/master/esp32_www) respectively. The first two examples can be built with CMake. The ESP32 example requires PlatformIO to build it.

The provided Visual Studio 2022 .NET 8 solution contains examples for generating the content at clasptree/www into output to the posix, esp32, or win32 example C++ applications. You're probably better off starting there, and examining the debug command lines involved in the associated Visual Studio project, such as clasptree, and also the www folder content, and the generated output.

The other projects also have examples in the debug settings such that you can examine the output from processing different files w/ different command line switches.

<a name="2.0"></a>
## Creating dynamic content with `.clasp` documents using the `clasp` executable

The clasp executable generates raw C/++ code suitable for injecting into an existing method body. It takes a `.clasp` page and switches as input, and produces the code at the specified output, or `stdout`.

<a name="2.1"></a>
## Concepts

A ClASP document is the main input, and combines document templating wiht C/++ code to render text.

This code can be used to emit that dynamic content over a suitable HTTP transport, such as a TCP socket. The way the code is structured, it calls two or more methods to render its content. 

The method names are defaulted to `response_block` and `response_expr`, the latter of which may be overloaded, and often is for C++ projects. These two methods get implemented by you in order to send the data to the output transport layer, probably in a platform dependent manner. Note that these method names can be changed via the command line. 

`void response_block(void* response__state)` takes a singular user defined context value `response_state` that is passed along with the call. Often this will be some structured data, or as simple as a socket handle. `void response_expr(? value, void* response_state)` takes a `value` and the `response__state` (as before) and renders the value over HTTP. The actual type of the `value` parameter is user defined, and in C++ you'll often have several overloads. 

Later we'll explore that, so you understand how it all ties together.

<a name="2.2"></a>
## ClASP document syntax and directives

If you've ever used Microsoft's ASP engine you'll find this syntax is very familiar. It is also similar to T4 templating syntax and PHP syntax in terms of the "context switching" but the "server side" language is C/++, not JScript, VBScript, or PHP.

Let's take a look at a simple dynamic page:
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
This page loops through every defined "alarm" and adds a checkbox for each alarm. If it is enabled, the checkbox becomes `checked`.


If used, the directives `<%@status %>` and `<%@header %>` must appear before any non-whitespace content.

`<%@status %>` has `code` (default 200) and `text` (default "OK") fields which indicate the status code and the status text to be sent. It also has the "auto-headers" field (defaults to true) which indicates that Content-Length+Content-Encoding or Transfer-Encoding headers should be generated as necessary.

`<%@header %>` has `name` and `value` fields which indicate the HTTP header name and value, and appends the specified header to the headers in the output.

`<% %>` code blocks contain C/++ code that can be used to render content. Consider the following snippet which emits 10 `<br />` tags to the output:
```html
<%for(int i = 0; i < 10; ++i) { %>
    <br />
<%} // end for %>
```
`<%= %>` expression blocks contain a C/C++ expression rather than statements. The expression gets passed to one of your response expression function (defaults to `response_expr`) as the first argument. You can create overloads of that function to accept various kinds of arguments from `<%= %>`. Consider the following snippet:
```html
<span>Name: <%=contact[i].name%></span><br />
<span>Age: <%=contact[i].age%></span><br />
<span>Email: <%=contact[i].email%></span><br />
```
Now assume your contact struct is something like:
```cpp
typedef struct {
    char name[128];
    unsigned char age;
    char email[512];
} contact_t;
```
In order to render this code your response expression function overloads will be called 3 times: Once for `.name` (`char*`), once for `.age` (`unsigned char`) and again for `.email` (`char *`). Each overload turns the expression into a string if necessary, and then sends it in HTTP chunked form.

<a name="2.3"></a>
## ClASP Command Line Interface

```
clasp

Generates C code from ASPish pages for use with embedded web servers

Usage:

clasp <inputfile> [ <outputfile> ] [ --block <block> ] [ --expr <expr> ] [ --state <state> ] [ --nostatus ]
    [ --headers <headers> ] [ --compress <compress> ]

<inputfile>      The input file
<outputfile>     The output file. Defaults to <stdout>
<block>          The function call to send a literal block to the client. Defaults to response_block
<expr>           The function call to send an expression to the client. Defaults to response_expr
<state>          The variable name that holds the user state to pass to the response functions. Defaults to response_state
--nostatus       Suppress the status line
<headers>        Indicates which headers should be generated (auto, or none). Defaults to auto
<compress>       Indicates the type of compression to use on static content: none, gzip, deflate, or auto. Defaults to auto

clasp --?

--?              Displays this screen
```

- The `<inputfile>` is the `.clasp` document to process, following the earlier described syntax.

- The optional `<outputfile>` is the output file to generate. If you don't indicate one, `stdout` is the destination. Note that this does not produce a valid standalone C/++ document, since it only generates method body code. You will need to inject the output from this application into an existing method, such as by using `#include` to put the content directly in a method body.

- `<block>` indicates the name of the method that will be called to send prepared HTTP encoded content to the transport. By default this will be `response_block`. The full signature is `void response_block(const char* data, size_t length, void* response_state)` and it is expected to be implement by you.

- `<expr>` indicates the name of of the method(s) that will be called to send expressions to the transport. The expression should be transformed into an HTTP chunked string before being sent to the `<block>` function. In C you'll likely have a singular method that takes a `const char*` string, and then do any necessary conversions in the .clasp C code before sending the result to `<%= %>`. In C++ the much more elegant solution is to create overloads of this method for each type you expect from your `<%= %>` code.

- `<state>` indicates the name of the (`void*`) state variable that's passed around to the functions. It is a user defined context variable that may be a struct of some kind, or may be as simple as a socket handle. Whatever it is, the implementation of the `<block>` method will use it to resolve the write to the actual socket or whatever HTTP transport you're using. It defaults to `response_state`.

- `--nostatus` indicates that the HTTP status line should be suppressed. This allows you to prepend your own status, plus potentially additional HTTP headers before the generated content.

- `<headers>` indicates how to generate the headers. `auto` means the potentially necessary headers `Content-Length`, `Transfer-Encoding` or `Content-Encoding` are generated for you as necessary. `none` means no headers are generated for you.

- `<compress>` indicates how to compress content when it is possible. Dynamic content cannot be precompressed, but `.clasp` pages containing only directives but no code or expression blocks - that is, static `.clasp` pages - can be compressed using deflate or gzip compression to save on program size and transport traffic. By default this is `auto` which indicates that whichever method yields the smallest result will be chosen. `none` indicates that content should never be compressed. The other options indicate a specific type of compression to use.

The `--?` option must be specified by itself and simply displays the above screen.

<a name="2.4"></a>
## Using ClASP generated code

Consider the following code generated from the page in [2.2](#2.2)

```cpp
response_block("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Type: text/h"
    "tml\r\n\r\nE2\r\n<!DOCTYPE html>\r\n<html>\r\n    <head>\r\n        <meta name=\"viewport\" co"
    "ntent=\"width=device-width, initial-scale=1.0\" />\r\n        <title>Alarm Control P"
    "anel</title>\r\n    </head>\r\n    <body>\r\n        <form method=\"get\" action=\".\">\r\n", 304, response_state);
for(size_t i = 0;i<alarm_count;++i) {

response_block("15\r\n\r\n            <label>\r\n", 27, response_state);
response_expr(i+1, response_state);
response_block("2F\r\n</label><input name=\"a\" type=\"checkbox\" value=\"\r\n", 53, response_state);
response_expr(i, response_state);
response_block("2\r\n\" \r\n", 7, response_state);
if(alarm_values[i]){
response_block("7\r\nchecked\r\n", 12, response_state);
}
response_block("9\r\n /><br />\r\n", 14, response_state);

}
response_block("A5\r\n\r\n            <input type=\"submit\" name=\"set\" value=\"set\" />\r"
    "\n            <input type=\"submit\" name=\"refresh\" value=\"get\" />\r\n        </form>"
    "\r\n    </body>\r\n</html>\r\n\r\n0\r\n\r\n", 176, response_state);
```

After inspecting it you can see that it's simply the backing C/++ code from the `.clasp` content, interspersed with calls to `response_block()` and `response_expr()`.

Once again, the easiest way to use this code is to simply `#include` this code inside an actual method *body*.

Now your goal should be to implement the functions so that they send the data over the transport.

Here are some examples for `response_block` for different platforms:

__[Win32 winsock]__
```cpp
void response_block(const char *data, size_t len, void *arg) {
    if (!data || !len) {
        return;
    }
    int sock = *((int*)arg);
    send(sock, data, len, 0);
}
```

__[POSIX sockets]__
```cpp
void response_block(const char *data, size_t len, void *arg) {
    if (!data || !len) {
        return;
    }
    int* pfd = (int*)arg;
    writen(*pfd,data,len);
}
```

__[ESP32 ESP-IDF httpd]__
```cpp
void response_block(const char* data, size_t len, void* arg) {
    if (!data || !len) {
        return;
    }
    httpd_req_t* r=(httpd_req_t*)arg;
    httpd_send(r,data,len);    
}
```

There are more steps to implementing `response_expr` but you can implement it on top of `response_block` so the same code works for every platform:

```cpp
static void response_send_chunked(const char *buffer, size_t buffer_len, void *arg) {
    char buf[64];
    if (buffer) {
        if(buffer_len) {
            itoa(buffer_len, buf, 16);
            strcat(buf, "\r\n");
            response_block(buf, strlen(buf), arg);
            response_block(buffer, buffer_len, arg);
            response_block("\r\n", 2, arg);
        }
        return;
    }
    response_block("0\r\n\r\n", 5, arg);
}

void response_expr(int expr, void* arg) {
    char buf[64];
    itoa(expr, buf, 10);
    response_send_chunked(buf, strlen(buf), arg);
}

void response_expr(float expr, void* arg) {
    char buf[64] = {0};
    sprintf(buf, "%0.2f", expr);
    // remove trailing zeroes
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
    response_send_chunked(buf, strlen(buf), arg);
}

void response_expr(unsigned char expr, void* arg) {
    char buf[64];
    sprintf(buf, "%d", (int)expr);
    response_send_chunked(buf, strlen(buf), arg);
}

// this one is probably the best choice for straight C
void response_expr(const char* expr, void* arg) {
    response_send_chunked(buf, strlen(buf), arg);
}
```

<a name="3.0"></a>
## Embedding static documents with the `clstat` executable

This tool produces similar output as `clasp` but was specifically designed to work with static content.

<a name="3.1"></a>
## Concepts

With static content over HTTP you will need the Content-Length, Content-Type and potentially the Content-Encoding headers along with the HTTP status line.

`clstat` computes all of that based on the content and file extension and produces `response_block` calls as with `clasp`.

The tool will compress content automatically if it results in a smaller final size.

<a name="3.2"></a>
## ClStat Command Line Interface

```
clstat

Generates C code to send static content over an HTTP connected socket

Usage:

clstat <input> [ <output> ] [ --code <code> ] [ --status <status> ] [ --nostatus ] [ --type <type> ]
    [ --compress <compress> ] [ --block <block> ] [ --state <state> ]

<input>        The input file to process.
<output>       The output to produce. Defaults to <stdout>
<code>         Indicates the HTTP status code. Defaults to 200
<status>       Indicates the HTTP status text. Defaults to OK
--nostatus     Indicates that the HTTP status line should be surpressed
<type>         Indicates the content type of the data. If unspecified it is determined from the file extension
<compress>     Indicates the type of compression to use: none, gzip, deflate, or auto. Defaults to auto
<block>        The function call to send a literal block to the client. Defaults to response_block
<state>        The variable name that holds the user state to pass to the response functions. Defaults to response_state

clstat --?

--?            Displays this screen
```
- `<input>` indicates the input file to process

- `<output>` indicates the output file to generate which defaults to `stdout`

- `<code>` indicates the HTTP status code to emit

- `<status>` indicates the HTTP status text to emit

- `--nostatus` indicates that the status line should be supressed, allowing for custom status lines and/or additional headers to be injected

- `<type>` indicates the MIME type of the content. By default the content type is determined based on the file extension.

- `<compress>` indicates the type of compression to use. `auto` indicates that the encoding that results in the smallest possible output should be used. `none` indicates that no compression should be used. The other options indicate a specific compression method

- `<block>` indicates the block write function to call to send content to the HTTP transport. Defaults to `response_block`

- `<state>` indicates the user defined context value to pass along to `response_block`. Defaults to `response_state`

The --? option must be specified by itself and simply displays the above screen.

<a name="3.3"></a>
## Using ClStat generated code

Using the `clstat` executable to produce generated code is almost exactly the same process as with `clasp` except that there is no need for any `response_expr` functions.

Refer to [section 2.4](#2.4) for details.

<a name="4.0"></a>
## Embedding an entire website using the `clasptree` executable

`clasptree` is a powerful tool that allows you to develop an entire web of content in a folder which it can take and generate a C/++ header for with all of the content that is present under the folder and its subfolders. It supports raw static content as well as dynamic and static `.clasp` files.

It also provides mapping facilities such that you can route an incoming HTTP path+query to to the appropriate content handler.

Essentially you develop your content in some kind of www root folder, and then point `clasptree` to that folder and it will generate a header you can include in your project that provides everything you need to route and deliver content over HTTP transports.

<a name="4.1"></a>
## Concepts

`clasptree` is an efficient way to embed an entire website into your application. You create the website locally on your PC, creating normal static content like JPGs, scripts and HTML, plus [`.clasp`](#2.0) files for any dynamic content like JSON or even server rendered HTML. You then point the `clasptree` executable at the root folder, with the desired command line options, and it gives you a single header library you can include in your projects in order to route requests by URL and otherwise deliver the entire website over any suitable HTTP transport.

<a name="4.1.1"></a>
### Static and dynamic content files

By default static files will be embedded and compressed with the appropriate HTTP headers and a 200 OK status line. The content type is auto-detected from each file extension.

Dynamic `.clasp` files can indicate the status and headers to use for their content, so they can be used to embed dynamic content or static content with a custom header or status line.

`.h` files are treated specially, and copied into the source tree as well as having an `#include` added in the code.

For each content file, a handler method is generated which takes the `response_state` user defined context parameter as an argument and delivers the content in that file.

Each file except `.h` files will be added to the handler routing list unless it begins with `.` in which case it will be treated as "hidden". The content handler method will still be generated, but no handler routing entry will be created for it, and it must be invoked manually by your code. This is primarily useful for creating HTTP error pages.

<a name="4.1.2"></a>
### URL response handler mappings

As mentioned in the previous section, a response handler mapping entry is generated for each content file in the website (.hidden files excluded), for example if you have `www/home.html` an entry will be create for `/home.html` that points to `void content_home_html(void* response_state)`.

By default `index.*` have an additional mapping where they are the "default file" of the directory and will be presented whenever '/' is indicated at that path. For example if you have `/api/index.clasp` a handler will be generated for `/api/index.clasp`, `/api/` and (optionally) `/api` as well.

You can get to the response handlers with the `response_handler_t response_handlers[RESPONSE_HANDLERS_COUNT]` array.

Normally, you'd just loop through that array comparing each `.path_encoded` entry with the incoming HTTP request's path using `strncmp()`, and then once you find a match, invoke `.handler`.

<a name="4.1.3"></a>
### URL response handler FSM matching

Optionally, you can generate a finite state machine as a more efficient and more powerful means of matching paths. If you specify this option on the command line, a method, `int response_handler_match(const char* path_and_query)` is generated which will give you the index into the `response_handlers[]` array for the given HTTP path and query part, or `-1` if it couldn't be found.

<a name="4.1.4"></a>
### Map files

The following is only available when FSM matching is indicated: 

Sometimes you may need aliases for content such that it can be returned from multiple paths. You also may want to match paths based on patterns, such as regular expressions. Map files allow you to create additional handler routing for content based on the input you give it.

A map file is specified on the command line, and it is line based syntax where each line creates another handler routing entry.

There are two pieces of data on each line, separated by a space. The first part is the actual filepath for the content to be generated. This file must be within the www root such that clasptree has generated a response handler function for it. The second part after the space is either a literal path in double quotes which will create an alias at that path for that response handler, or a regular expression in single quotes that matches a pattern.

Comments start with `#` and continue to the rest of the line

The regular expressions are simple DFA, so they cannot backtrack and lazy matching is not supported. The `^` and `$` anchors are always implicit, and cannot be specified explicitly.


Here is an example map file:

```
.fs_api.clasp '(/api/spiffs/(.*))|(/api/sdcard/(.*))' # wildcard match for /api/spiffs/* or /api/sdcard/*
index.clasp "/default.html" # literal alias to map /default.html to the content handler for index.clasp
```
Note that the content handler paths are relative to the website root folder.

<a name="4.1.5"></a>
### Additional includes

If you place .h files under your website's folder tree they will be copied into your project, with the same relative path being created in your output directory, and then an `#include` for the file will be placed in the output header file.

For example, placing an `http_application.h` in the root of your website will cause it to be copied to your output directory and an `#include "http_application.h"` line will be added in your output file. Similarly `/api/http_api.h` would cause `./api/http_api.h` to be copied to your output directory and `#include "api/http_application.h"` line will be added.

<a name="4.1.6"></a>
### Prologue and epilogue files

Prologue and epilogue files contain C/++ code that will be added to the beginning and end of each handler function, respectively. You can indicate them on the command line, and the contents are copied verbatim into each method.

<a name="4.2"></a>
## ClASP-Tree Command Line Interface
```
clasptree

Generates dynamic ClASP content and static content from a directory tree

Usage:

clasptree <input> [ <output> ] [ --block <block> ] [ --expr <expr> ] [ --state <state> ] [ --prefix <prefix> ]
    [ --prologue <prologue> ] [ --epilogue <epilogue> ] [ --handlers <handlers> ] [ --index <index> ] [ --nostatus ]
    [ --handlerfsm ] [ --urlmap <urlmap> ]

<input>         The root directory of the site. Defaults to the current directory
<output>        The output file to generate. Defaults to <stdout>
<block>         The function call to send a literal block to the client. Defaults to response_block
<expr>          The function call to send an expression to the client. Defaults to response_expr
<state>         The variable name that holds the user state to pass to the response functions. Defaults to response_state
<prefix>        The method prefix to use, if specified.
<prologue>      The file to insert into each method before any code
<epilogue>      The file to insert into each method after any code
<handlers>      Indicates whether to generate no handler entries (none), default entries (default) or extended (extended)
        handlers. None doesn't emit any. Default emits them in accordance with their paths, plus resoving indexes based
        on <index>. Extended does this and also adds path/ trailing handlers
<index>         Generate / default handlers for files matching this wildcard. Defaults to "index.*"
--nostatus      Suppress the status headers
--handlerfsm    Generate a finite state machine that can be used for matching headers
<urlmap>        Generates handler mappings from a map file. <headersfsm> must be specified

clasptree --?

--?             Displays this screen
```
- `<input>` indicates the folder that is the root directory of the website content. This is basically your wwwroot for your website.

- `<output>` indicates the output file to generate. By default this goes to `stdout`

- `<block>` indicates the name of the method to call to send content to the HTTP transport. You are expected to implement this method. Defaults to `response_block` (see [section 2.4](#2.4))

- `<expr>` indicates the name of the methods to call to send an expression to the HTTP transport. You are expected to implement this method(s). Defaults to `response_expr` (see [section 2.4](#2.4))

- `<state>` indicates the name of the user defined context argument passed to each response handler, and passed through to `<block>` and `<expr>` methods. Defaults to `response_state`

- `<prefix>` indicates a prefix to prepend to every generated method, variable and type introduced by `clasptree`. Defaults to no prefix. An example would be `http_` which would cause `http_response_handler_t http_response_handlers[HTTP_RESPONSE_HANDLER_COUNT]` to be generated.

- `<prologue>` indicates the file containing the prologue code to insert at the start of each response handler method. (see [section 4.1.6](#4.1.6))

- `<epilogue>` indicates the file containing the epilogue code to insert at the end of each response handler method. (see [section 4.1.6](#4.1.6))

- `<handlers>` controls which handlers are generated based on the content at the website. By `default` one handler is created for each exposed path, such that each file in the website that is visible will have a handler entry exposed for it. If `none` is indicated, no handler array will be generated. Any index files (as indicated by the `<index>` CLI argument) will have an additional handler associated with them. For example `/index.clasp` would create `/home/index.clasp` and `/home/`. If `extended` is specified, 3 handlers are created for most index files. Such that from above `/home` would also be added.

- `<index>` indicates the filesystem wildcard match to use for default files. This option defaults to `index.*` such that any file named `index` will have a default path associated with it as described in `<handlers>` just above.

- `--nostatus` indicates that the HTTP status line is to be omitted from generated content. This allows your code to insert additional HTTP headers and custom status lines for each bit of content.

- `--handlerfsm` indicates that a finite state machine and corresponding `int response_handler_match(const char* path_and_query)` should be generated that can be used to match incoming requests to the appropriate content handler. (see [section 4.1.3](#4.1.3))

- `<urlmap>` indicates a map file which will be used to create additional routing for custom URLs for the handler FSM. (see [section 4.1.4](#4.1.4))

- `--?` must be specified by itself and simply displays the above screen


<a name="4.3"></a>
## Using the generated header file

Typically you'd `#include` your generated header in your main C/++ source file (example: `main.cpp`). There is an extra step here, and that is making a `#define` first. If your output is `www_content.h` you'd need to `#define WWW_CONTENT_IMPLEMENTATION` just before `#include "www_content.h"` so that it will link. Do this in exactly one and only one source file. However, you can include `www_content.h` anywhere else you need it, but without that `#define`. If you forget the define, the project won't link. This technique allows one file to contain both the prototypes and implementation code. Note that the define is based on the name of the output file. In the header itself, you will find the actual name to use commented near the top.

In your main.cpp:

```cpp
#include <stdint.h>
#include <stddef.h>

// declare the necessary function prototypes before including your content
extern void response_block(const char* data, size_t len, void* response_state);

// which of these overloads you need depends on what you use between <%= %> blocks:
extern void response_expr(const char* expr, void* response_state); // almost always this one
extern void response_expr(int expr, void* response_state); // send an int
extern void response_expr(float expr, void* response_state); // send a float
extern void response_expr(bool expr, void* response_state); // send a bool
// in order to link, this define must be present before the include in exactly one source file
#define WWW_CONTENT_IMPLEMENTATION
#include "www_content.h"
```

You'll need to implement each of those functions yourself. Only `response_block` is platform specific. The rest can build on that. Here is some example code:

```cpp
// this just sends straight to a socket with no transformation
void response_block(const char* buffer, size_t buffer_len, void* response_state) {
    if (!buffer || !buffer_len) {
        return;
    }
    // the response_state is a socket handle in this case
    // it's whatever you need to be, in practice
    int socket_descriptor  = *(int*)response_state;
    send(socket_descriptor, buffer, buffer_len, 0);
}
// send an HTTP chunked response, or buffer = NULL for the terminator
static void response_send_chunked(const char* buffer,
                               size_t buffer_len, void* response_state_) {
    char buf[64];
    if (buffer) {
        if(buffer_len) {
            itoa(buffer_len, buf, 16);
            strcat(buf, "\r\n");
            responnse_block(buf,strlen(buf), response_state);
            responnse_block(buffer,buffer_len, response_state);
            responnse_block("\r\n",2, response_state);
        }
        return;
    }
    responnse_block("0\r\n\r\n", 5, response_state);
}

// these expression overloads transform to a string and then send chunked
void response_expr(const char* expr, void* response_state) {
    if (!expr || !*expr) {
        return;
    }
    response_send_chunked(expr, strlen(expr), response_state);
}
void response_expr(int expr, void* response_state) {
    char buf[64];
    snprintf(buf, sizeof(buf), "%d", expr);
    response_send_chunked(buf, strlen(buf), response_state);
}
void response_expr(float expr, void* response_state) {
    char buf[64];
    snprintf(buf, sizeof(buf), "%0.2f", expr);
    response_send_chunked(buf, strlen(buf), response_state);
}
```
Note that most of this code can be copied directly into your project, with only minor modifications to `response_block`.

That takes care of the boilerplate include and prototyping plus the low level communication and allows both static and dynamic content to be rendered to a socket. Again, only `response_block` is platform specific, and only `response_block` cares about the contents of `response_state`.

Now that we have that, next we need a way to route incoming HTTP request paths to the appropriate handler.

There are two ways to do it, depending on whether or not you generated the [handler finite state machine](#4.1.3).

The first way involves looping through the handlers using something like `strncmp()`

```cpp
int handler_index = -1;
const char* query_part = strchr(request_path,'?');
size_t len = (query_part == nullptr) ? strlen(request_path) : query_part - request_path + 1;
for(int i = 0; i < RESPONSE_HANDLER_COUNT; ++i) {
    reponse_handler_t& h = response_handler[i];
    if(0==strncmp(h.path_encoded, request_path, len)) {
        handler_index = i;
        break;
    }
}
// if handler index is -1 it isn't found
```
Note that we compared with `.path_encoded` so that the encoded path can be compared almost directly. Note also that we clipped the `?` query part during the comparison.

If you generated the finite state machine, this is even easier, as a function does it for you. It's more efficient especially when you have a lot of paths to compare, and it's more flexible, allowing for [map files](#4.1.4) to be used.

```cpp
int handler_index = response_handler_match(request_path);
// if handler index is -1 it isn't found
```
Note that we did not even have to clip the query part.

One you have the handler index you can route to the appropriate handler method, which has the prototype of `void content_handler(void* response_state)`

Calling this method causes the content in that handler to be written out ultimately by using the `response_block` implementation which the other methods delegate to.

```cpp
if(handler_index!=-1) {
    response_handlers[handler_index].handler(response_state);
} else {
    content_404_clasp(response_state); // assuming you have a `.404.clasp` in the root of your website
}
```
