# ClASP-Tree

ClStat: The C Language static content generator

ClStat is a C and C++ oriented HTTP response generator that takes input files and generates method calls to send them over a socket to a browser.

Usage:
```
clasptree v1.0.0.0

Usage:

clasptree <input> [ <output> ] [ /block <block> ] [ /expr <expr> ] [ /state <state> ] [ /prefix <prefix> ]
    [ /prologue <prologue> ] [ /epilogue <epilogue> ]

<input>        The root directory of the site. Defaults to the current directory
<output>       The output file to generate. Defaults to <stdout>
<block>        The function call to send a literal block to the client. Defaults to response_block
<expr>         The function call to send an expression to the client. Defaults to response_expr
<state>        The variable name that holds the user state to pass to the response functions. Defaults to response_state
<prefix>       The method prefix to use, if specified.
<prologue>     The file to insert into each method before any code
<epilogue>     The file to insert into each method after any code

clasptree /?

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
