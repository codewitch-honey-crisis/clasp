# ClStat

ClStat: The C Language static content generator

ClStat is a C and C++ oriented HTTP response generator that takes input files and generates method calls to send them over a socket to a browser.

Usage:
```
clstat

Generates C code to send static content over an HTTP connected socket

Usage:

clstat <input> [ <output> ] [ /code <code> ] [ /status <status> ] [ /nostatus ] [ /type <type> ]
    [ /compress <compress> ] [ /block <block> ] [ /state <state> ]

<input>        The input file to process.
<output>       The output to produce. Defaults to <stdout>
<code>         Indicates the HTTP status code. Defaults to 200
<status>       Indicates the HTTP status text. Defaults to OK
/nostatus      Indicates that the HTTP status line should be surpressed
<type>         Indicates the content type of the data.
<compress>     Indicates the type of compression to use: none, gzip, deflate, or auto. Defaults to auto
<block>        The function call to send a literal block to the client. Defaults to response_block
<state>        The variable name that holds the user state to pass to the response functions. Defaults to response_state

clstat /?

/?             Displays this screen
```

First, consider the following trivial text document `hello.txt`. It simply reads `hello`: 
```
hello
```
ClStat will generate this HTTP response:

```
HTTP/1.1 200 OK
Content-Type: text/plain
Content-Length: 5

hello
```

The first line is default but can be altered with `<status>` `<code>` and `/nostatus` options.
The second line's value was detected from the file extension `.txt`
The `Content-Length` header is always computed.

Here's the actual code it generates by executing `clstat hello.txt`:
```cpp
response_block("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 5\r\n\r\nhello", 69, response_state);
```

You then write a simple wrapper functions from above to send data out on a socket. 

The example presented above was text, but binary content will be generated using byte arrays. 

Content is compressed via the `<compress>` option which defaults to `auto`. `auto` in turn chooses whichever method yields the least size.

You can specify the content-type with `<type>`

Content is included in the project so you can try it.
